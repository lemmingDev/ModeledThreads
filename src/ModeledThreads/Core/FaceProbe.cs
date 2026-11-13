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

    /// <summary>The face's length along the axis, in millimetres.</summary>
    public double LengthMm => (HighCm - LowCm) * 10.0;

    /// <summary>The face's radius, in millimetres.</summary>
    public double RadiusMm => RadiusCm * 10.0;

    /// <summary>How the two ends meet, in the order the axis runs.</summary>
    public ThreadEndKind EndAt(int side) => side == 0 ? LowEnd : HighEnd;
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
            NormalIsOutward = normalIsOutward
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

        try
        {
            Point onFace = face.PointOnFace;
            double[]? normal = FaceNormal(face, onFace);
            if (normal is not null)
            {
                double t = Project(onFace, basePoint, axis);
                Point axisPoint = geometry.CreatePoint(
                    basePoint.X + axis.X * t,
                    basePoint.Y + axis.Y * t,
                    basePoint.Z + axis.Z * t);

                double rx = onFace.X - axisPoint.X;
                double ry = onFace.Y - axisPoint.Y;
                double rz = onFace.Z - axisPoint.Z;
                double radialLength = Math.Sqrt(rx * rx + ry * ry + rz * rz);
                if (radialLength > 1e-9)
                {
                    double dot = (normal[0] * rx + normal[1] * ry + normal[2] * rz) / radialLength;
                    if (Math.Abs(dot) > 0.5)
                    {
                        // The face's normal points away from the axis: the material
                        // is inside the cylinder, so this is a shaft.
                        normalIsOutward = dot > 0;
                        return dot < 0;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not read the face's normal: {ex.Message}");
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

                // The normal points away from the material: with the end's
                // direction it means there is air beyond the end, so the groove
                // may run out into it.
                if (dot > 0)
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
