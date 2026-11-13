using Inventor;
using ModeledThreads.Utilities;
using Point = Inventor.Point;

namespace ModeledThreads.Core;

/// <summary>
/// Editing a thread that is already in the model: find the face it was cut on,
/// take its features out, and cut it again from the new numbers.
/// </summary>
public static class ThreadEditor
{
    /// <summary>
    /// Finds the cylindrical face a thread was cut on from the record's axis and
    /// radius. The face is looked up again rather than remembered: taking the
    /// thread out can change which face is which, so the geometry is the only
    /// thing that can be trusted across the edit.
    /// </summary>
    public static Face? FindFace(PartDocument document, ThreadRecord record, TransientGeometry geometry)
    {
        UnitVector axis = geometry.CreateUnitVector(record.AxisX, record.AxisY, record.AxisZ);
        Point basePoint = geometry.CreatePoint(record.BaseX, record.BaseY, record.BaseZ);

        Face? best = null;
        double bestOffset = 0.01; // a tenth of a millimetre

        foreach (SurfaceBody body in document.ComponentDefinition.SurfaceBodies)
        {
            foreach (Face face in body.Faces)
            {
                if (face.SurfaceType != SurfaceTypeEnum.kCylinderSurface || face.Geometry is not Cylinder cylinder)
                {
                    continue;
                }

                if (Math.Abs(cylinder.Radius - record.RadiusCm) > 1e-3)
                {
                    continue;
                }

                UnitVector faceAxis = cylinder.AxisVector;
                double parallel = (faceAxis.X * axis.X) + (faceAxis.Y * axis.Y) + (faceAxis.Z * axis.Z);
                if (Math.Abs(Math.Abs(parallel) - 1.0) > 1e-4)
                {
                    continue;
                }

                // How far the face's axis lies from the recorded one.
                Point point = cylinder.BasePoint;
                double dx = point.X - basePoint.X;
                double dy = point.Y - basePoint.Y;
                double dz = point.Z - basePoint.Z;
                double along = (dx * axis.X) + (dy * axis.Y) + (dz * axis.Z);
                double offset = Math.Sqrt(
                    ((dx - (along * axis.X)) * (dx - (along * axis.X)))
                    + ((dy - (along * axis.Y)) * (dy - (along * axis.Y)))
                    + ((dz - (along * axis.Z)) * (dz - (along * axis.Z))));

                if (offset < bestOffset)
                {
                    bestOffset = offset;
                    best = face;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Takes the thread out and cuts it again with the new numbers, as one
    /// transaction: if anything fails, the old thread is left as it was.
    /// </summary>
    /// <param name="makePlan">Builds the new plan from the record's face.</param>
    public static ThreadPlan Replace(
        Inventor.Application app,
        PartDocument document,
        ThreadRecord existing,
        ThreadRecord updated,
        Func<FaceProbe, ThreadPlan> makePlan)
    {
        Transaction transaction = app.TransactionManager.StartTransaction(
            document.InventorDocument, $"Edit thread {updated.Designation}");

        try
        {
            Remove(document, existing.ThreadId);

            // The surface the thread was cut into has to be back for the new cut
            // to have something to bite into. It is only a check: the new cut is
            // placed by the record's geometry, not by a face reference.
            if (FindFace(document, updated, app.TransientGeometry) is null)
            {
                throw new ThreadPlanException(
                    "The surface the thread was cut into is no longer in the part, "
                    + "so the thread was left as it was.");
            }

            FaceProbe probe = FaceProbe.FromRecord(updated, app.TransientGeometry);
            ThreadPlan plan = makePlan(probe);

            var builder = new ThreadBuilder(app, document, probe);
            builder.Build(plan, updated);

            transaction.End();
            return plan;
        }
        catch
        {
            try
            {
                transaction.Abort();
            }
            catch
            {
                // the original error is the useful one
            }

            throw;
        }
    }

    private static void Remove(PartDocument document, string threadId)
    {
        PartComponentDefinition definition = document.ComponentDefinition;

        foreach (object feature in ThreadMetadata.Features(definition, threadId))
        {
            string name = ThreadMetadata.NameOf(feature);

            try
            {
                // Anything that depends on the thread is kept rather than swept
                // away with it: a broken feature the user can see is better than
                // a silently deleted one.
                ThreadMetadata.Delete(feature);
                Log.Info($"Removed '{name}' for the edit.");
            }
            catch (Exception ex)
            {
                Log.Warn($"Could not remove '{name}': {ex.Message}");
                throw new ThreadPlanException(
                    $"'{name}' cannot be replaced: something else in the part depends on it.");
            }
        }
    }
}
