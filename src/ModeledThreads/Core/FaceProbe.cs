using Inventor;
using ModeledThreads.Utilities;
using Point = Inventor.Point;

namespace ModeledThreads.Core;

/// <summary>
/// What a cylindrical face looks like: its axis, its radius, how far it runs and
/// what each of its ends meets. The thread plan is worked out from this.
/// </summary>
public sealed class FaceProbe
{
    /// <summary>The cylinder's axis direction.</summary>
    public required UnitVector Axis { get; init; }

    /// <summary>A point on the axis; the extent is measured from here.</summary>
    public required Point BasePoint { get; init; }

    /// <summary>The cylinder's radius, in centimetres.</summary>
    public required double RadiusCm { get; init; }

    /// <summary>The face's extent along the axis at its low end, in centimetres.</summary>
    public required double LowCm { get; init; }

    /// <summary>The face's extent along the axis at its high end, in centimetres.</summary>
    public required double HighCm { get; init; }

    /// <summary>True when the face is the wall of a hole.</summary>
    public required bool Internal { get; init; }

    /// <summary>What the face's low end meets.</summary>
    public required ThreadEndKind LowEnd { get; init; }

    /// <summary>What the face's high end meets.</summary>
    public required ThreadEndKind HighEnd { get; init; }

    /// <summary>True when the face's normal points out of the material.</summary>
    public required bool NormalIsOutward { get; init; }

    /// <summary>
    /// The solid body the face belongs to. A part can be several bodies, and a
    /// cut left to itself is aimed at the first of them, so the thread's cuts
    /// are aimed at this one. It is null when the probe came from a record and
    /// there is no body to point at.
    /// </summary>
    public SurfaceBody? Body { get; init; }

    /// <summary>The face's length along the axis, in millimetres.</summary>
    public double LengthMm => (HighCm - LowCm) * 10.0;

    /// <summary>The face's radius, in millimetres.</summary>
    public double RadiusMm => RadiusCm * 10.0;

    /// <summary>How the two ends meet, in the order the axis runs.</summary>
    public ThreadEndKind EndAt(int side) => side == 0 ? LowEnd : HighEnd;

    /// <summary>
    /// The face a thread was cut on, rebuilt from what the thread recorded. An
    /// edit does not need to find the face again in the model: the thread's
    /// cut is placed by geometry, and the record has all of it.
    /// </summary>
    public static FaceProbe FromRecord(ThreadRecord record, TransientGeometry geometry)
    {
        return new FaceProbe
        {
            Axis = geometry.CreateUnitVector(record.AxisX, record.AxisY, record.AxisZ),
            BasePoint = geometry.CreatePoint(record.BaseX, record.BaseY, record.BaseZ),
            RadiusCm = record.RadiusCm,
            LowCm = record.LowCm,
            HighCm = record.HighCm,
            Internal = record.Internal,
            LowEnd = record.LowEnd,
            HighEnd = record.HighEnd,
            NormalIsOutward = record.NormalIsOutward
        };
    }
}

/// <summary>
/// Reads a cylindrical face and works out what a thread on it has to deal with.
/// </summary>
public static class FaceProbeReader
{
    /// <summary>How close to an end a face has to be to count as being at it, in centimetres.</summary>
    private const double EndToleranceCm = 0.02;

    public static FaceProbe? Probe(PartDocument document, Face face, TransientGeometry geometry)
    {
        if (face.SurfaceType != SurfaceTypeEnum.kCylinderSurface)
        {
            return null;
        }

        if (face.Geometry is not Cylinder cylinder)
        {
            return null;
        }

        UnitVector axis = cylinder.AxisVector;
        Point basePoint = cylinder.BasePoint;

        (double low, double high) = Extent(face, basePoint, axis);
        if (high - low <= 1e-6)
        {
            return null;
        }

        bool normalIsOutward = true;
        bool isInternal = IsInternal(face, cylinder, basePoint, axis, geometry, out normalIsOutward);

        ThreadEndKind lowEnd = ClassifyEnd(
            document, face, basePoint, axis, low, cylinder.Radius, isInternal, -1);
        ThreadEndKind highEnd = ClassifyEnd(
            document, face, basePoint, axis, high, cylinder.Radius, isInternal, +1);

        Log.Info(
            $"Face probed: radius {cylinder.Radius * 10:0.###} mm, extent {low * 10:0.###}..{high * 10:0.###} mm, " +
            $"internal={isInternal}, ends {lowEnd}/{highEnd}.");

        return new FaceProbe
        {
            Axis = axis,
            BasePoint = basePoint,
            RadiusCm = cylinder.Radius,
            LowCm = low,
            HighCm = high,
            Internal = isInternal,
            LowEnd = lowEnd,
            HighEnd = highEnd,
            NormalIsOutward = normalIsOutward,
            Body = BodyOf(face)
        };
    }

    /// <summary>
    /// The face's extent along the axis, from the points on its edges.
    /// </summary>
    private static (double Low, double High) Extent(Face face, Point basePoint, UnitVector axis)
    {
        double low = double.MaxValue;
        double high = double.MinValue;

        void Add(Point p)
        {
            double t = Project(p, basePoint, axis);
            if (t < low) low = t;
            if (t > high) high = t;
        }

        foreach (Edge edge in face.Edges)
        {
            try
            {
                Add(edge.PointOnEdge);
            }
            catch
            {
                // an edge with no usable point is skipped
            }

            try
            {
                Vertex? start = edge.StartVertex;
                if (start != null)
                {
                    Add(start.Point);
                }

                Vertex? stop = edge.StopVertex;
                if (stop != null)
                {
                    Add(stop.Point);
                }
            }
            catch
            {
                // vertices are a bonus, not required
            }
        }

        if (low > high)
        {
            // Fall back on the face's box.
            Box box = face.Evaluator.RangeBox;
            Add(box.MinPoint);
            Add(box.MaxPoint);
        }

        return (low, high);
    }

    /// <summary>
    /// Whether the face is the wall of a hole. The face's normal tells which way
    /// the material lies: out of the cylinder for a shaft, into it for a hole.
    /// </summary>
    private static bool IsInternal(
        Face face,
        Cylinder cylinder,
        Point basePoint,
        UnitVector axis,
        TransientGeometry geometry,
        out bool normalIsOutward)
    {
        normalIsOutward = true;

        // A whole cylinder's face starts and ends at its seam, and the normal
        // read there can come back with the wrong side of the surface: a bore
        // then reads as a shaft, which is the one mistake that must not happen.
        // The middle of the face's parameter range is away from the seam, so
        // the normals are read there, and the answer is the one they agree on.
        int inward = 0;
        int outward = 0;

        foreach (double fraction in new[] { 0.5, 0.25, 0.75 })
        {
            Point? sample = PointAt(face, geometry, fraction) ?? face.PointOnFace;
            double[]? normal = FaceNormal(face, sample);
            if (normal is null)
            {
                continue;
            }

            double t = Project(sample, basePoint, axis);
            double rx = sample.X - (basePoint.X + axis.X * t);
            double ry = sample.Y - (basePoint.Y + axis.Y * t);
            double rz = sample.Z - (basePoint.Z + axis.Z * t);
            double radialLength = Math.Sqrt((rx * rx) + (ry * ry) + (rz * rz));
            if (radialLength < 1e-9)
            {
                continue;
            }

            double dot = ((normal[0] * rx) + (normal[1] * ry) + (normal[2] * rz)) / radialLength;
            if (Math.Abs(dot) <= 0.5)
            {
                continue;
            }

            // The face's normal points away from the axis: the material is
            // inside the cylinder, so this is a shaft.
            if (dot > 0)
            {
                outward++;
            }
            else
            {
                inward++;
            }
        }

        if (inward + outward > 0)
        {
            normalIsOutward = outward >= inward;
            return inward > outward;
        }

        // Fall back on the geometry: a hole's face is a reversed surface.
        try
        {
            return face.IsParamReversed;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// What the face's end at the given position meets: air (free), a shoulder
    /// or a closed end.
    /// </summary>
    private static ThreadEndKind ClassifyEnd(
        PartDocument document,
        Face face,
        Point basePoint,
        UnitVector axis,
        double endPosition,
        double radiusCm,
        bool isInternal,
        int direction)
    {
        foreach (Edge edge in face.Edges)
        {
            double t;
            try
            {
                t = Project(edge.PointOnEdge, basePoint, axis);
            }
            catch
            {
                continue;
            }

            if (Math.Abs(t - endPosition) > EndToleranceCm)
            {
                continue;
            }

            foreach (Face neighbour in edge.Faces)
            {
                if (neighbour.TransientKey == face.TransientKey)
                {
                    continue;
                }

                if (neighbour.SurfaceType != SurfaceTypeEnum.kPlaneSurface)
                {
                    continue;
                }

                if (neighbour.Geometry is not Plane plane)
                {
                    continue;
                }

                double planePosition = Project(plane.RootPoint, basePoint, axis);
                if (Math.Abs(planePosition - endPosition) > EndToleranceCm)
                {
                    continue;
                }

                double[]? normal = FaceNormal(neighbour, neighbour.PointOnFace);
                if (normal is null)
                {
                    continue;
                }

                double dot = normal[0] * axis.X * direction
                             + normal[1] * axis.Y * direction
                             + normal[2] * axis.Z * direction;

                // A flat face at the end of a hole can be a blind bottom, the
                // shoulder of a counterbore, or the joint where two bodies are
                // butted together. If a smaller hole carries on through it
                // there is more hole beyond, so the thread runs up to it and
                // needs a relief, rather than stopping short of a bottom. This
                // is asked before the normal is looked at, because at a joint
                // the neighbour can be the other body's face and its normal
                // points back into the part, which would read as a bottom.
                if (isInternal && HasOpening(document, basePoint, axis, endPosition, radiusCm))
                {
                    return ThreadEndKind.Shoulder;
                }

                // The normal points away from the material: with the end's
                // direction it means there is air beyond the end, so the groove
                // may run out into it. Material of another body lying behind the
                // end is not air, though: the thread must stop at the joint
                // rather than cut into the body behind.
                if (dot > 0 && !BodyExtendsBeyond(document, basePoint, axis, endPosition, direction))
                {
                    return ThreadEndKind.Free;
                }

                return isInternal ? ThreadEndKind.Closed : ThreadEndKind.Shoulder;
            }
        }

        // No flat face at the end: a chamfer or a fillet. Look at the body's
        // own extent: nothing beyond the end means air there.
        bool beyond = BodyExtendsBeyond(document, basePoint, axis, endPosition, direction);
        if (!beyond)
        {
            return ThreadEndKind.Free;
        }

        return isInternal ? ThreadEndKind.Closed : ThreadEndKind.Shoulder;
    }

    /// <summary>The body the face is part of, or null if it cannot be read.</summary>
    private static SurfaceBody? BodyOf(Face face)
    {
        try
        {
            return face.SurfaceBody;
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not tell which body the face belongs to: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// True when a smaller hole carries on through the flat face at the end of
    /// the given hole. Every face on the end plane is looked at, not only the
    /// one the hole's own wall meets: a part can have two bodies butted
    /// together, and the smaller hole can be bored into the one behind.
    /// </summary>
    private static bool HasOpening(
        PartDocument document,
        Point basePoint,
        UnitVector axis,
        double endPosition,
        double radiusCm)
    {
        try
        {
            foreach (SurfaceBody body in document.ComponentDefinition.SurfaceBodies)
            {
                foreach (Face face in body.Faces)
                {
                    if (face.SurfaceType != SurfaceTypeEnum.kPlaneSurface
                        || face.Geometry is not Plane plane)
                    {
                        continue;
                    }

                    if (Math.Abs(Project(plane.RootPoint, basePoint, axis) - endPosition) > EndToleranceCm)
                    {
                        continue;
                    }

                    foreach (Edge edge in face.Edges)
                    {
                        if (edge.Geometry is not Circle circle
                            || circle.Radius >= radiusCm - 1e-3)
                        {
                            continue;
                        }

                        // It has to be the same hole: a small circle off to one
                        // side of the end plane is some other feature.
                        if (RadialDistance(circle.Center, basePoint, axis) < 1e-3)
                        {
                            return true;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not look for an opening at the end of the face: {ex.Message}");
        }

        return false;
    }

    /// <summary>A point's distance from the axis through the base point.</summary>
    private static double RadialDistance(Point point, Point basePoint, UnitVector axis)
    {
        double dx = point.X - basePoint.X;
        double dy = point.Y - basePoint.Y;
        double dz = point.Z - basePoint.Z;
        double along = (dx * axis.X) + (dy * axis.Y) + (dz * axis.Z);

        return Math.Sqrt(
            ((dx - (along * axis.X)) * (dx - (along * axis.X)))
            + ((dy - (along * axis.Y)) * (dy - (along * axis.Y)))
            + ((dz - (along * axis.Z)) * (dz - (along * axis.Z))));
    }

    /// <summary>
    /// Whether any of the body's faces reach beyond the given plane along the
    /// axis, which means there is material there.
    /// </summary>
    private static bool BodyExtendsBeyond(
        PartDocument document, Point basePoint, UnitVector axis, double endPosition, int direction)
    {
        try
        {
            foreach (SurfaceBody body in document.ComponentDefinition.SurfaceBodies)
            {
                for (int i = 1; i <= body.Faces.Count; i++)
                {
                    Face f = body.Faces[i];
                    double t = Project(f.PointOnFace, basePoint, axis);
                    if (direction > 0 ? t > endPosition + EndToleranceCm : t < endPosition - EndToleranceCm)
                    {
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not check the body beyond the face's end: {ex.Message}");
        }

        return false;
    }

    /// <summary>The face's normal at a point, as an x,y,z array.</summary>
    /// <summary>
    /// A point on the face at the given fraction of its parameter range, or
    /// null when the range cannot be read.
    /// </summary>
    private static Point? PointAt(Face face, TransientGeometry geometry, double fraction)
    {
        try
        {
            Box2d range = face.Evaluator.ParamRangeRect;
            double u = range.MinPoint.X + ((range.MaxPoint.X - range.MinPoint.X) * fraction);
            double v = (range.MinPoint.Y + range.MaxPoint.Y) / 2.0;
            double[] parameters = { u, v };
            double[] output = new double[3];
            face.Evaluator.GetPointAtParam(ref parameters, ref output);
            return geometry.CreatePoint(output[0], output[1], output[2]);
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not read a point on the face: {ex.Message}");
            return null;
        }
    }

    private static double[]? FaceNormal(Face face, Point point)
    {
        try
        {
            double[] input = { point.X, point.Y, point.Z };
            double[] output = new double[3];
            face.Evaluator.GetNormalAtPoint(ref input, ref output);
            if (output.Length < 3)
            {
                return null;
            }

            double length = Math.Sqrt(output[0] * output[0] + output[1] * output[1] + output[2] * output[2]);
            if (length < 1e-9)
            {
                return null;
            }

            return output;
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not read a face's normal: {ex.Message}");
            return null;
        }
    }

    /// <summary>A point's place along the axis, measured from the base point.</summary>
    private static double Project(Point p, Point basePoint, UnitVector axis) =>
        (p.X - basePoint.X) * axis.X
        + (p.Y - basePoint.Y) * axis.Y
        + (p.Z - basePoint.Z) * axis.Z;
}
