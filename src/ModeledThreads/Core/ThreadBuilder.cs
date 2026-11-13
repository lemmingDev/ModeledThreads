using Inventor;
using ModeledThreads.Utilities;
using Point = Inventor.Point;

namespace ModeledThreads.Core;

/// <summary>
/// Cuts a planned thread into a part: the groove is a profile sketched in a
/// plane through the thread's axis and swept along it as a coil, and the ends
/// are finished with revolved cuts.
/// </summary>
public sealed class ThreadBuilder
{
    private readonly Inventor.Application _app;
    private readonly PartDocument _document;
    private readonly FaceProbe _probe;

    /// <summary>The names of the features the last build made.</summary>
    public List<string> CreatedFeatures { get; } = new();

    public ThreadBuilder(Inventor.Application app, PartDocument document, FaceProbe probe)
    {
        _app = app;
        _document = document;
        _probe = probe;
    }

    /// <summary>Builds the thread.</summary>
    public void Build(ThreadPlan plan)
    {
        PartComponentDefinition definition = _document.ComponentDefinition;
        TransientGeometry geometry = _app.TransientGeometry;

        UnitVector axis = _probe.Axis;
        UnitVector sweep = geometry.CreateUnitVector(
            axis.X * plan.Direction, axis.Y * plan.Direction, axis.Z * plan.Direction);
        UnitVector radial = Perpendicular(sweep, geometry);

        // The thread starts at the face's end the plan says, on the axis.
        double threadStartCm = plan.Direction > 0 ? _probe.LowCm : _probe.HighCm;
        Point threadStart = OnAxis(threadStartCm);

        // Every work plane and axis here is made as a construction feature: the
        // thread only needs them to be built, and a user does not want the planes
        // it happened to use left lying across the part.
        WorkAxis workAxis = Stage("work axis",
            () => definition.WorkAxes.AddFixed(threadStart, sweep, true));
        workAxis.Name = $"Modeled Thread Axis {plan.Designation}";

        // The revolved end cuts get an axis of their own: a coil that consumes its
        // inputs takes the axis with it.

        // ---- the groove ------------------------------------------------------
        // The sweep starts a pitch before a free end, so the groove reaches full
        // depth exactly where the face ends and its lead-in lies in the air.
        double sweepStartCm = plan.SweepStartMm / 10.0;
        Point sweepStart = OnAxis(sweepStartCm);

        double pitchCm = plan.PitchMm / 10.0;
        double leadCm = plan.LeadMm / 10.0;
        double heightCm = Math.Abs(plan.SweepEndMm - plan.SweepStartMm) / 10.0;

        for (int start = 0; start < Math.Max(1, plan.Starts); start++)
        {
            // Each start of a multi-start thread is the same helix a fraction of a
            // turn further round.
            UnitVector startRadial = RotateAbout(radial, sweep, 360.0 * start / Math.Max(1, plan.Starts), geometry);

            WorkPlane plane = Stage("profile plane",
                () => definition.WorkPlanes.AddFixed(sweepStart, startRadial, sweep, true));
            PlanarSketch sketch = Stage("profile sketch", () => definition.Sketches.Add(plane, false));

            Profile? drawn = Stage("profile", () => DrawProfile(sketch, plan, geometry));
            if (drawn is null)
            {
                throw new InvalidOperationException("The thread's profile could not be drawn.");
            }

            Profile profile = drawn;

            CoilFeature coil = Stage("coil cut", () => definition.Features.CoilFeatures.AddByPitchAndHeight(
                profile,
                workAxis,
                leadCm,
                heightCm,
                PartFeatureOperationEnum.kCutOperation,
                false,
                !plan.RightHanded));

            Stage("coil name", () => coil.Name = $"Modeled Thread {plan.Designation}" + (plan.Starts > 1 ? $" ({start + 1})" : string.Empty));
            CreatedFeatures.Add(coil.Name);
        }

        // ---- the ends ---------------------------------------------------------
        // The lead-in cone at the free end the thread starts at.
        if (plan.Tip is { } tip)
        {
            WorkAxis tipAxis = Stage("lead-in axis",
                () => definition.WorkAxes.AddFixed(threadStart, sweep, true));
            CutTipCone(plan, tip, threadStart, sweep, radial, tipAxis);
        }

        // The relief at a shoulder.
        if (plan.Relief is { } relief)
        {
            WorkAxis reliefAxis = Stage("relief axis",
                () => definition.WorkAxes.AddFixed(threadStart, sweep, true));
            CutRelief(plan, relief, threadStart, sweep, radial, reliefAxis);
        }
    }

    /// <summary>
    /// Draws the groove's profile: a trapezoid in the plane through the axis,
    /// with the root at one end and the mouth past the surface at the other.
    /// </summary>
    private static Profile? DrawProfile(PlanarSketch sketch, ThreadPlan plan, TransientGeometry geometry)
    {
        if (plan.Section.Count < 3)
        {
            return null;
        }

        SketchLines lines = sketch.SketchLines;

        Point2d Point(int index)
        {
            ProfilePoint p = plan.Section[index % plan.Section.Count];
            return geometry.CreatePoint2d(p.R / 10.0, p.Z / 10.0);
        }

        // The first line, then one per pair of points, and last the closing line
        // back onto the first line's own start point. That start point has to be
        // reused, not a new point at the same place, or Inventor does not see the
        // loop as closed and refuses the profile.
        SketchLine first = lines.AddByTwoPoints(Point(0), Point(1));
        SketchLine last = first;

        for (int i = 1; i < plan.Section.Count - 1; i++)
        {
            last = lines.AddByTwoPoints(last.EndSketchPoint, Point(i + 1));
        }

        lines.AddByTwoPoints(last.EndSketchPoint, first.StartSketchPoint);

        return sketch.Profiles.AddForSolid();
    }

    /// <summary>
    /// The 45° lead-in at a free end (ISO 4753): a cone that takes the thread's
    /// last teeth with it, so the end is whole.
    /// </summary>
    private void CutTipCone(
        ThreadPlan plan,
        TipCone tip,
        Point threadStart,
        UnitVector sweep,
        UnitVector radial,
        WorkAxis workAxis)
    {
        double depthCm = tip.DepthMm / 10.0;
        double faceCm = _probe.RadiusCm;

        // A tenth of a millimetre past the surface (or into the hole), so the cut
        // clears it instead of running coincident with it, which the rebuild
        // refuses once the thread has been cut there.
        const double clear = 0.01;

        // The cone's profile, in the plane through the axis: the end's face, the
        // surface and the cone's slope.
        (double R0, double R1, double R2) = tip.Internal
            ? (faceCm - clear, faceCm + depthCm, faceCm - clear)
            : (faceCm - depthCm, faceCm + clear, faceCm + clear);

        WorkPlane plane = Stage("lead-in plane",
            () => _document.ComponentDefinition.WorkPlanes.AddFixed(threadStart, radial, sweep, true));
        PlanarSketch sketch = Stage("lead-in sketch",
            () => _document.ComponentDefinition.Sketches.Add(plane, false));

        TransientGeometry geometry = _app.TransientGeometry;

        Stage("lead-in lines", () =>
        {
            SketchLines lines = sketch.SketchLines;

            SketchLine l1 = lines.AddByTwoPoints(geometry.CreatePoint2d(R0, 0), geometry.CreatePoint2d(R1, 0));
            SketchLine l2 = lines.AddByTwoPoints(l1.EndSketchPoint, geometry.CreatePoint2d(R2, depthCm));
            lines.AddByTwoPoints(l2.EndSketchPoint, l1.StartSketchPoint);
        });

        Profile profile = Stage("lead-in profile", () => sketch.Profiles.AddForSolid());

        RevolveFeature revolve = Stage("lead-in cut", () =>
            _document.ComponentDefinition.Features.RevolveFeatures.AddFull(
                profile, workAxis, PartFeatureOperationEnum.kCutOperation));
        revolve.Name = $"Modeled Thread Lead-in {plan.Designation}";
        CreatedFeatures.Add(revolve.Name);

        Log.Info($"Lead-in cone cut at the start: depth {tip.DepthMm:0.###} mm.");
    }

    /// <summary>
    /// The relief at a shoulder: one pitch high, as deep as the thread's cut, so
    /// the last turn runs out before the shoulder.
    /// </summary>
    private void CutRelief(
        ThreadPlan plan,
        ShoulderRelief relief,
        Point threadStart,
        UnitVector sweep,
        UnitVector radial,
        WorkAxis workAxis)
    {
        // A tenth of a millimetre each way, so neither face of the cut runs
        // coincident with the surface or the hole it meets.
        double r0 = (relief.InnerRadiusMm - 0.1) / 10.0;
        double r1 = (relief.OuterRadiusMm + 0.1) / 10.0;
        double t0 = relief.StartMm / 10.0;
        double t1 = relief.EndMm / 10.0;

        if (r0 <= 0 || t1 - t0 <= 1e-6)
        {
            return;
        }

        WorkPlane plane = Stage("relief plane",
            () => _document.ComponentDefinition.WorkPlanes.AddFixed(threadStart, radial, sweep, true));
        PlanarSketch sketch = Stage("relief sketch",
            () => _document.ComponentDefinition.Sketches.Add(plane, false));

        TransientGeometry geometry = _app.TransientGeometry;

        Stage("relief lines", () =>
        {
            SketchLines lines = sketch.SketchLines;

            SketchLine l1 = lines.AddByTwoPoints(geometry.CreatePoint2d(r0, t0), geometry.CreatePoint2d(r1, t0));
            SketchLine l2 = lines.AddByTwoPoints(l1.EndSketchPoint, geometry.CreatePoint2d(r1, t1));
            SketchLine l3 = lines.AddByTwoPoints(l2.EndSketchPoint, geometry.CreatePoint2d(r0, t1));
            lines.AddByTwoPoints(l3.EndSketchPoint, l1.StartSketchPoint);
        });

        Profile profile = Stage("relief profile", () => sketch.Profiles.AddForSolid());

        RevolveFeature revolve = Stage("relief cut", () =>
            _document.ComponentDefinition.Features.RevolveFeatures.AddFull(
                profile, workAxis, PartFeatureOperationEnum.kCutOperation));
        revolve.Name = $"Modeled Thread Relief {plan.Designation}";
        CreatedFeatures.Add(revolve.Name);

        Log.Info(
            $"Shoulder relief cut: {relief.EndMm - relief.StartMm:0.###} mm high, " +
            $"r {relief.InnerRadiusMm:0.###}..{relief.OuterRadiusMm:0.###} mm.");
    }

    /// <summary>A stage of the build, named so a failure says where it happened.</summary>
    private static T Stage<T>(string name, Func<T> action)
    {
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"The step '{name}' failed: {ex.Message}", ex);
        }
    }

    private static void Stage(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"The step '{name}' failed: {ex.Message}", ex);
        }
    }

    /// <summary>A point on the thread's axis at the given place along it.</summary>
    private Point OnAxis(double positionCm)
    {
        UnitVector axis = _probe.Axis;
        return _app.TransientGeometry.CreatePoint(
            _probe.BasePoint.X + axis.X * positionCm,
            _probe.BasePoint.Y + axis.Y * positionCm,
            _probe.BasePoint.Z + axis.Z * positionCm);
    }

    /// <summary>A unit vector at right angles to the given one.</summary>
    private static UnitVector Perpendicular(UnitVector axis, TransientGeometry geometry)
    {
        double ax = Math.Abs(axis.X) < 0.9 ? 1.0 : 0.0;
        double ay = Math.Abs(axis.X) < 0.9 ? 0.0 : 1.0;

        // The cross product of (ax, ay, 0) with the axis.
        double x = ay * axis.Z;
        double y = -ax * axis.Z;
        double z = ax * axis.Y - ay * axis.X;

        double length = Math.Sqrt(x * x + y * y + z * z);
        if (length < 1e-9)
        {
            return geometry.CreateUnitVector(1, 0, 0);
        }

        return geometry.CreateUnitVector(x / length, y / length, z / length);
    }

    /// <summary>The given vector turned about the axis by the given angle.</summary>
    private static UnitVector RotateAbout(UnitVector vector, UnitVector axis, double degrees, TransientGeometry geometry)
    {
        double radians = degrees * Math.PI / 180.0;
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);

        // Rodrigues' rotation formula.
        double dot = vector.X * axis.X + vector.Y * axis.Y + vector.Z * axis.Z;
        double cx = axis.Y * vector.Z - axis.Z * vector.Y;
        double cy = axis.Z * vector.X - axis.X * vector.Z;
        double cz = axis.X * vector.Y - axis.Y * vector.X;

        double x = vector.X * cos + cx * sin + axis.X * dot * (1 - cos);
        double y = vector.Y * cos + cy * sin + axis.Y * dot * (1 - cos);
        double z = vector.Z * cos + cz * sin + axis.Z * dot * (1 - cos);

        double length = Math.Sqrt(x * x + y * y + z * z);
        if (length < 1e-9)
        {
            return vector;
        }

        return geometry.CreateUnitVector(x / length, y / length, z / length);
    }
}
