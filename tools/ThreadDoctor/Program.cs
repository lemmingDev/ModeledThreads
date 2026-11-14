using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Inventor;
using ModeledThreads.Core;

// Reaches a particular Inventor instance through the running object table and
// reads, or re-cuts, a modelled thread in the document that is open there.
//
//   ThreadDoctor <pid>                 show the documents and the threads
//   ThreadDoctor <pid> --apply <name>  re-cut the named thread in place
//
// The document is left unsaved; a copy of it as it was is saved first.

if (args.Length == 0 || !int.TryParse(args[0], out int pid))
{
    Console.WriteLine("usage: ThreadDoctor <inventor pid> [--apply <thread name>]");
    return 1;
}

bool apply = args.Length > 2 && args[1] == "--apply";
string threadName = apply ? args[2] : string.Empty;
bool describe = args.Contains("--ends");

Inventor.Application? app = Rot.FindApplication(pid);
if (app is null)
{
    Console.WriteLine($"No Inventor instance with pid {pid} was found in the running object table.");
    return 1;
}

Console.WriteLine($"Inventor: {app.Caption}");
Console.WriteLine($"Documents: {app.Documents.Count}");
foreach (Document document in app.Documents)
{
    Console.WriteLine($"  '{document.DisplayName}'  '{document.FullFileName}'  dirty={document.Dirty}");
}

Document? target = null;
foreach (Document document in app.Documents)
{
    if (document.DisplayName.StartsWith("thread", StringComparison.OrdinalIgnoreCase))
    {
        target = document;
        break;
    }
}

target ??= app.ActiveDocument;

if (target is not PartDocument part)
{
    Console.WriteLine("No part document is open in that instance.");
    return 1;
}

Console.WriteLine();
Console.WriteLine($"Part: {part.DisplayName}");

PartComponentDefinition definition = part.ComponentDefinition;
TransientGeometry geometry = app.TransientGeometry;

var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
foreach (PartFeature feature in definition.Features)
{
    if (!ThreadMetadata.TryReadFrom(feature, out ThreadRecord? record, out _) || record is null)
    {
        continue;
    }

    if (!seen.Add(record.ThreadId))
    {
        continue;
    }

    List<object> group = ThreadMetadata.Features(definition, record.ThreadId);
    Console.WriteLine();
    Console.WriteLine($"Thread {record.ThreadId}: {record.Designation}");
    Console.WriteLine($"  features: {string.Join(", ", group.Select(ThreadMetadata.NameOf))}");
    Console.WriteLine($"  internal={record.Internal} clearance={record.ClearanceMm} starts={record.Starts}");
    Console.WriteLine($"  chamferStart={record.ChamferStart} chamferEnd={record.ChamferEnd} reverse={record.ReverseStart}");
    Console.WriteLine($"  recorded ends: {record.LowEnd}/{record.HighEnd}");

    Face? face = ThreadEditor.FindFace(part, record, geometry);
    FaceProbe? probe = face is null ? null : FaceProbeReader.Probe(part, face, geometry);
    Console.WriteLine(probe is null
        ? "  the face it was cut on was not found"
        : $"  re-read face: r {probe.RadiusMm:0.###} mm, {probe.LowCm * 10:0.###}..{probe.HighCm * 10:0.###} mm, "
          + $"internal={probe.Internal}, ends {probe.LowEnd}/{probe.HighEnd}");

    if (describe)
    {
        DescribeFaces(part, record.RadiusCm, app);
    }

    if (!apply || !string.Equals(feature.Name, threadName, StringComparison.OrdinalIgnoreCase))
    {
        continue;
    }

    if (probe is null)
    {
        Console.WriteLine("  nothing was changed: the face could not be read.");
        return 1;
    }

    double before = part.ComponentDefinition.MassProperties.Volume * 1000.0;

    string backup = $@"C:\Temp\{System.IO.Path.GetFileNameWithoutExtension(part.DisplayName)}-before-fix.ipt";
    try
    {
        part.SaveAs(backup, true);
        Console.WriteLine($"  a copy as it was: {backup}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  the copy could not be saved: {ex.Message}");
        return 1;
    }

    if (!ThreadCatalog.TryParse(record.Designation, out ThreadSize size, out string error))
    {
        Console.WriteLine($"  the designation cannot be read: {error}");
        return 1;
    }

    int starts = Math.Max(1, record.Starts);
    double clearance = record.ClearanceMm;
    double length = record.LengthMm;

    ThreadPlan MakePlan(FaceProbe p)
    {
        // The same choice the dialog makes: the free end is the start unless
        // the thread was cut from the other one.
        int automatic = ChooseStartSide(p);
        int side = record.ReverseStart ? 1 - automatic : automatic;

        return ThreadPlanBuilder.Build(new ThreadPlanOptions
        {
            Form = size.Form,
            Designation = record.Designation,
            MajorMm = size.MajorMm,
            PitchMm = size.PitchMm,
            LeadMm = size.PitchMm * starts,
            Internal = p.Internal,
            ClearanceMm = clearance,
            RightHanded = record.RightHanded,
            Starts = starts,
            FaceRadiusMm = p.RadiusMm,
            FaceLowMm = p.LowCm * 10.0,
            FaceHighMm = p.HighCm * 10.0,
            StartSide = side,
            LengthMm = length,
            LowEndKind = p.LowEnd,
            HighEndKind = p.HighEnd,
            ChamferStart = record.ChamferStart,
            ChamferEnd = record.ChamferEnd
        });
    }

    ThreadPlan plan = ThreadEditor.Replace(app, part, record, record, MakePlan);

    double after = part.ComponentDefinition.MassProperties.Volume * 1000.0;
    string relief = plan.Relief is null
        ? "no relief"
        : $"relief {(plan.Relief.Value.EndMm - plan.Relief.Value.StartMm):0.###} mm high";
    Console.WriteLine($"  re-cut: {relief}");
    Console.WriteLine($"  volume {before:0.#} -> {after:0.#} mm3, {plan.Turns:0.###} turns");

    foreach (object item in ThreadMetadata.Features(definition, record.ThreadId))
    {
        Console.WriteLine($"  feature: {ThreadMetadata.NameOf(item)}");
    }

    Console.WriteLine("  the document is left unsaved");
}

return 0;

/// <summary>
/// Every cylindrical face of the thread's radius, its ends, and the faces and
/// edges each end meets. This is the reading the end classification works from,
/// so it shows why an end came out the way it did.
/// </summary>
static void DescribeFaces(PartDocument part, double radiusCm, Inventor.Application app)
{
    foreach (SurfaceBody body in part.ComponentDefinition.SurfaceBodies)
    {
        foreach (Face face in body.Faces)
        {
            if (face.SurfaceType != SurfaceTypeEnum.kCylinderSurface || face.Geometry is not Cylinder cylinder)
            {
                continue;
            }

            if (Math.Abs(cylinder.Radius - radiusCm) > 1e-3)
            {
                continue;
            }

            FaceProbe? probe = FaceProbeReader.Probe(part, face, app.TransientGeometry);
            Console.WriteLine();
            Console.WriteLine($"face {face.TransientKey}: r {cylinder.Radius * 10:0.###} mm");
            if (probe is null)
            {
                Console.WriteLine("  could not be read");
                continue;
            }

            Console.WriteLine($"  axis {probe.Axis.X:0.###},{probe.Axis.Y:0.###},{probe.Axis.Z:0.###}");
            Console.WriteLine($"  extent {probe.LowCm * 10:0.###}..{probe.HighCm * 10:0.###} mm, internal={probe.Internal}");
            Console.WriteLine($"  ends {probe.LowEnd}/{probe.HighEnd}");

            DescribeEnd(face, probe, low: true);
            DescribeEnd(face, probe, low: false);
        }
    }
}

/// <summary>What one end of a face meets: its edges, the faces beside them, and
/// the edges of those faces.</summary>
static void DescribeEnd(Face face, FaceProbe probe, bool low)
{
    double end = low ? probe.LowCm : probe.HighCm;

    double Project(Point p) =>
        (p.X - probe.BasePoint.X) * probe.Axis.X
        + (p.Y - probe.BasePoint.Y) * probe.Axis.Y
        + (p.Z - probe.BasePoint.Z) * probe.Axis.Z;

    Console.WriteLine($"  {(low ? "low" : "high")} end at {end * 10:0.###} mm:");

    foreach (Edge edge in face.Edges)
    {
        double t;
        try
        {
            t = Project(edge.PointOnEdge);
        }
        catch
        {
            continue;
        }

        if (Math.Abs(t - end) > 0.01)
        {
            continue;
        }

        Console.WriteLine($"    edge {GeometryName(edge.Geometry)}, at {t * 10:0.###} mm");

        foreach (Face neighbour in edge.Faces)
        {
            if (neighbour.TransientKey == face.TransientKey)
            {
                continue;
            }

            string extra = string.Empty;
            if (neighbour.Geometry is Plane plane)
            {
                extra = $", plane at {Project(plane.RootPoint) * 10:0.###} mm, "
                        + $"normal {plane.Normal.X * probe.Axis.X + plane.Normal.Y * probe.Axis.Y + plane.Normal.Z * probe.Axis.Z:0.###}";
            }

            Console.WriteLine($"      neighbour {neighbour.SurfaceType}{extra}");

            foreach (Edge inner in neighbour.Edges)
            {
                string radius = inner.Geometry is Circle circle ? $", r {circle.Radius * 10:0.###} mm" : string.Empty;
                string where = string.Empty;
                try
                {
                    where = $", at {Project(inner.PointOnEdge) * 10:0.###} mm";
                }
                catch
                {
                    // an edge that cannot be placed is still worth naming
                }

                Console.WriteLine($"        its edge {GeometryName(inner.Geometry)}{radius}{where}");
            }
        }
    }
}

static string GeometryName(object? geometry)
{
    return geometry switch
    {
        Circle => "Circle",
        Line => "Line",
        EllipseFull => "Ellipse",
        EllipticalArc => "EllipticalArc",
        BSplineCurve => "BSplineCurve",
        null => "null",
        _ => geometry.GetType().Name
    };
}

static int ChooseStartSide(FaceProbe probe)
{
    bool lowFree = probe.LowEnd == ThreadEndKind.Free;
    bool highFree = probe.HighEnd == ThreadEndKind.Free;

    if (lowFree && !highFree)
    {
        return 0;
    }

    if (highFree && !lowFree)
    {
        return 1;
    }

    return 0;
}

/// <summary>The running object table, for reaching one particular Inventor.</summary>
internal static class Rot
{
    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable table);

    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(int reserved, out IBindCtx context);

    public static Inventor.Application? FindApplication(int pid)
    {
        if (GetRunningObjectTable(0, out IRunningObjectTable table) != 0)
        {
            return null;
        }

        table.EnumRunning(out IEnumMoniker enumMoniker);

        var monikers = new IMoniker[1];
        IntPtr fetched = IntPtr.Zero;

        while (enumMoniker.Next(1, monikers, fetched) == 0)
        {
            IMoniker moniker = monikers[0];

            try
            {
                CreateBindCtx(0, out IBindCtx context);
                moniker.GetDisplayName(context, null, out string name);

                if (name is null || !name.EndsWith("-" + pid, StringComparison.Ordinal))
                {
                    continue;
                }

                table.GetObject(moniker, out object held);

                if (held is Inventor.Application application)
                {
                    return application;
                }

                if (held is Document document && document.Parent is Inventor.Application owner)
                {
                    return owner;
                }

                Console.WriteLine($"  pid {pid}: the object is {held.GetType().FullName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  pid {pid}: {ex.Message}");
            }
            finally
            {
                Marshal.ReleaseComObject(moniker);
            }
        }

        return null;
    }
}
