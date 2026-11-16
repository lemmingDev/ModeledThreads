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

// --designation changes the size the thread is re-cut to, which is how a thread
// cut with the dialog's default can be given the size that was actually wanted.
int designationIndex = Array.IndexOf(args, "--designation");
string? designation = designationIndex >= 0 && designationIndex + 1 < args.Length
    ? args[designationIndex + 1]
    : null;

Inventor.Application? app = Rot.FindApplication(pid);
if (app is null)
{
    Console.WriteLine($"No Inventor instance with pid {pid} was found in the running object table.");
    return 1;
}

if (args.Contains("--quit"))
{
    foreach (Document document in app.Documents)
    {
        Console.WriteLine($"  closing '{document.DisplayName}' without saving");
        document.Close(true);
    }

    Console.WriteLine("  quitting Inventor");
    app.Quit();
    return 0;
}

int openIndex = Array.IndexOf(args, "--open");
if (openIndex >= 0 && openIndex + 1 < args.Length)
{
    string path = args[openIndex + 1];
    Document? open = null;
    foreach (Document document in app.Documents)
    {
        if (string.Equals(document.FullFileName, path, StringComparison.OrdinalIgnoreCase))
        {
            open = document;
            break;
        }
    }

    if (open is null)
    {
        Console.WriteLine($"Opening '{path}'");
        open = app.Documents.Open(path, true);
    }

    open.Activate();
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

if (args.Contains("--docs"))
{
    foreach (Document document in app.Documents)
    {
        if (document is not PartDocument listing)
        {
            continue;
        }

        Console.WriteLine();
        Console.WriteLine($"Document: '{listing.DisplayName}'  '{listing.FullFileName}'  dirty={listing.Dirty}");
        Console.WriteLine($"  volume {listing.ComponentDefinition.MassProperties.Volume * 1000.0:0.#} mm3");
        foreach (PartFeature feature in listing.ComponentDefinition.Features)
        {
            Console.WriteLine($"  feature: {feature.Name}");
        }
    }

    return 0;
}

int createIndex = Array.IndexOf(args, "--create");
if (createIndex >= 0 && createIndex + 1 < args.Length)
{
    return CreateThread(app, part, args, args[createIndex + 1]);
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

    ThreadRecord updated = designation is null
        ? record
        : new ThreadRecord
        {
            ThreadId = record.ThreadId,
            Designation = designation,
            LengthMm = record.LengthMm,
            ClearanceMm = record.ClearanceMm,
            Starts = record.Starts,
            RightHanded = record.RightHanded,
            ChamferStart = record.ChamferStart,
            ChamferEnd = record.ChamferEnd,
            ReverseStart = record.ReverseStart,
            Internal = record.Internal,
            AxisX = record.AxisX,
            AxisY = record.AxisY,
            AxisZ = record.AxisZ,
            BaseX = record.BaseX,
            BaseY = record.BaseY,
            BaseZ = record.BaseZ,
            RadiusCm = record.RadiusCm,
            LowCm = record.LowCm,
            HighCm = record.HighCm,
            LowEnd = record.LowEnd,
            HighEnd = record.HighEnd,
            NormalIsOutward = record.NormalIsOutward
        };

    if (!ThreadCatalog.TryParse(updated.Designation, out ThreadSize size, out string error))
    {
        Console.WriteLine($"  the designation cannot be read: {error}");
        return 1;
    }

    int starts = Math.Max(1, updated.Starts);
    double clearance = updated.ClearanceMm;
    double length = updated.LengthMm;

    ThreadPlan MakePlan(FaceProbe p)
    {
        // The same choice the dialog makes: the free end is the start unless
        // the thread was cut from the other one.
        int automatic = ChooseStartSide(p);
        int side = record.ReverseStart ? 1 - automatic : automatic;

        return ThreadPlanBuilder.Build(new ThreadPlanOptions
        {
            Form = size.Form,
            Designation = updated.Designation,
            MajorMm = size.MajorMm,
            PitchMm = size.PitchMm,
            LeadMm = size.PitchMm * starts,
            Internal = p.Internal,
            ClearanceMm = clearance,
            RightHanded = updated.RightHanded,
            Starts = starts,
            FaceRadiusMm = p.RadiusMm,
            FaceLowMm = p.LowCm * 10.0,
            FaceHighMm = p.HighCm * 10.0,
            StartSide = side,
            LengthMm = length,
            LowEndKind = p.LowEnd,
            HighEndKind = p.HighEnd,
            ChamferStart = updated.ChamferStart,
            ChamferEnd = updated.ChamferEnd
        });
    }

    ThreadPlan plan = ThreadEditor.Replace(app, part, record, updated, MakePlan);

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
/// Cuts a new thread on a face found by radius and side, without the dialog.
/// It is how a part whose unsaved threads were lost can be put back, and how a
/// build can be tried on a real part without pressing buttons.
/// </summary>
static int CreateThread(Inventor.Application app, PartDocument part, string[] args, string designation)
{
    if (!ThreadCatalog.TryParse(designation, out ThreadSize size, out string error))
    {
        Console.WriteLine($"  the designation cannot be read: {error}");
        return 1;
    }

    int Value(string name, int fallback)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out int number)
            ? number
            : fallback;
    }

    double Number(string name, double fallback)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length
               && double.TryParse(args[index + 1], System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture, out double number)
            ? number
            : fallback;
    }

    int radiusIndex = Array.IndexOf(args, "--radius");
    double radiusMm = radiusIndex >= 0 && radiusIndex + 1 < args.Length
                      && double.TryParse(args[radiusIndex + 1], System.Globalization.NumberStyles.Float,
                          System.Globalization.CultureInfo.InvariantCulture, out double wanted)
        ? wanted
        : 0.0;
    bool external = args.Contains("--external");

    TransientGeometry geometry = app.TransientGeometry;
    Face? chosen = null;
    FaceProbe? probe = null;
    double longest = -1.0;

    foreach (SurfaceBody body in part.ComponentDefinition.SurfaceBodies)
    {
        foreach (Face face in body.Faces)
        {
            if (face.SurfaceType != SurfaceTypeEnum.kCylinderSurface || face.Geometry is not Cylinder cylinder)
            {
                continue;
            }

            double radius = cylinder.Radius * 10.0;
            if (Math.Abs(radius - radiusMm) > 0.05)
            {
                continue;
            }

            FaceProbe? reading = FaceProbeReader.Probe(part, face, geometry);
            if (reading is null || reading.Internal == external || reading.LengthMm <= longest)
            {
                continue;
            }

            chosen = face;
            probe = reading;
            longest = reading.LengthMm;
        }
    }

    if (chosen is null || probe is null)
    {
        Console.WriteLine($"  no {(external ? "outside" : "inside")} cylindrical face of radius {radiusMm} mm was found");
        return 1;
    }

    Console.WriteLine($"  face: r {probe.RadiusMm:0.###} mm, {probe.LowCm * 10:0.###}..{probe.HighCm * 10:0.###} mm, "
        + $"internal={probe.Internal}, ends {probe.LowEnd}/{probe.HighEnd}");

    int starts = Math.Max(1, Value("--starts", 1));
    double clearance = Number("--clearance", 0.0);
    double length = Number("--length", 0.0);
    bool chamferStart = !args.Contains("--no-chamfer-start");
    bool chamferEnd = !args.Contains("--no-chamfer-end");
    bool reverse = args.Contains("--reverse");

    int automatic = (probe.LowEnd == ThreadEndKind.Free && probe.HighEnd != ThreadEndKind.Free) ? 0
        : (probe.HighEnd == ThreadEndKind.Free && probe.LowEnd != ThreadEndKind.Free) ? 1
        : 0;

    var record = new ThreadRecord
    {
        ThreadId = Guid.NewGuid().ToString("N"),
        Designation = designation,
        LengthMm = length,
        ClearanceMm = clearance,
        Starts = starts,
        RightHanded = !args.Contains("--left"),
        ChamferStart = chamferStart,
        ChamferEnd = chamferEnd,
        ReverseStart = reverse,
        Internal = probe.Internal,
        AxisX = probe.Axis.X,
        AxisY = probe.Axis.Y,
        AxisZ = probe.Axis.Z,
        BaseX = probe.BasePoint.X,
        BaseY = probe.BasePoint.Y,
        BaseZ = probe.BasePoint.Z,
        RadiusCm = probe.RadiusCm,
        LowCm = probe.LowCm,
        HighCm = probe.HighCm,
        LowEnd = probe.LowEnd,
        HighEnd = probe.HighEnd,
        NormalIsOutward = probe.NormalIsOutward
    };

    ThreadPlan plan = ThreadPlanBuilder.Build(new ThreadPlanOptions
    {
        Form = size.Form,
        Designation = designation,
        MajorMm = size.MajorMm,
        PitchMm = size.PitchMm,
        LeadMm = size.PitchMm * starts,
        Internal = probe.Internal,
        ClearanceMm = clearance,
        RightHanded = record.RightHanded,
        Starts = starts,
        FaceRadiusMm = probe.RadiusMm,
        FaceLowMm = probe.LowCm * 10.0,
        FaceHighMm = probe.HighCm * 10.0,
        StartSide = reverse ? 1 - automatic : automatic,
        LengthMm = length,
        LowEndKind = probe.LowEnd,
        HighEndKind = probe.HighEnd,
        ChamferStart = chamferStart,
        ChamferEnd = chamferEnd
    });

    double before = part.ComponentDefinition.MassProperties.Volume * 1000.0;

    Transaction transaction = app.TransactionManager.StartTransaction(
        part.InventorDocument, $"Create thread {designation}");

    try
    {
        var builder = new ThreadBuilder(app, part, probe);
        builder.Build(plan, record);
        transaction.End();
    }
    catch
    {
        transaction.Abort();
        throw;
    }

    double after = part.ComponentDefinition.MassProperties.Volume * 1000.0;
    Console.WriteLine($"  created {plan.Designation}: {plan.Turns:0.###} turns, {plan.SweepLengthMm:0.###} mm, "
        + (plan.Relief is { } relief
            ? $"relief {relief.EndMm - relief.StartMm:0.###} mm high"
            : "no relief")
        + $", volume {before:0.#} -> {after:0.#} mm3, left unsaved");
    return 0;
}

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
