namespace ModeledThreads.Core;

/// <summary>How one end of a thread meets the end of the face it is cut on.</summary>
public enum ThreadEndKind
{
    /// <summary>No material past it: the groove may run out into the air.</summary>
    Free,

    /// <summary>Material past it at a larger radius (a flange or a head): a shoulder.</summary>
    Shoulder,

    /// <summary>Material past it at the same radius (a blind hole's bottom): a closed end.</summary>
    Closed,

    /// <summary>The thread stops inside the face.</summary>
    Inside
}

/// <summary>One point of the groove's profile: the radius from the axis and the axial offset.</summary>
public readonly record struct ProfilePoint(double R, double Z);

/// <summary>The 45° lead-in cone at a free end (ISO 4753).</summary>
public readonly record struct TipCone(double RadiusMm, double DepthMm, bool Internal);

/// <summary>The relief cut at a shoulder.</summary>
public readonly record struct ShoulderRelief(
    double StartMm,
    double EndMm,
    double InnerRadiusMm,
    double OuterRadiusMm);

/// <summary>Something about the request cannot be built.</summary>
public sealed class ThreadPlanException(string message) : Exception(message);

/// <summary>Everything asked for a thread, in millimetres.</summary>
public sealed class ThreadPlanOptions
{
    public ThreadFormKind Form { get; set; } = ThreadFormKind.Metric;

    public string Designation { get; set; } = string.Empty;

    /// <summary>The nominal (major) diameter.</summary>
    public double MajorMm { get; set; }

    /// <summary>The pitch: the distance between neighbouring grooves.</summary>
    public double PitchMm { get; set; }

    /// <summary>The lead: how far the thread advances in one revolution.</summary>
    public double LeadMm { get; set; }

    /// <summary>True for a thread cut into a hole.</summary>
    public bool Internal { get; set; }

    /// <summary>The extra material cut away at the groove's root, radially.</summary>
    public double ClearanceMm { get; set; }

    public bool RightHanded { get; set; } = true;

    /// <summary>The number of starts: 1 for an ordinary thread.</summary>
    public int Starts { get; set; } = 1;

    /// <summary>The radius of the cylindrical face the thread is cut on.</summary>
    public double FaceRadiusMm { get; set; }

    /// <summary>The face's extent along its axis, at the low end.</summary>
    public double FaceLowMm { get; set; }

    /// <summary>The face's extent along its axis, at the high end.</summary>
    public double FaceHighMm { get; set; }

    /// <summary>Which end of the face the thread starts at: 0 the low end, 1 the high end.</summary>
    public int StartSide { get; set; }

    /// <summary>How far the thread runs from its start. Zero: the whole face.</summary>
    public double LengthMm { get; set; }

    public ThreadEndKind LowEndKind { get; set; } = ThreadEndKind.Free;

    public ThreadEndKind HighEndKind { get; set; } = ThreadEndKind.Free;

    /// <summary>The 45° lead-in at the free end the thread starts at.</summary>
    public bool ChamferStart { get; set; }

    /// <summary>The relief at a shoulder.</summary>
    public bool ChamferEnd { get; set; }
}

/// <summary>
/// A thread resolved against a face: the groove's profile, where the sweep runs,
/// and what has to be cut besides the sweep itself.
///
/// Everything here is plain arithmetic in millimetres along the face's axis, so
/// it can be checked without Inventor.
/// </summary>
public sealed class ThreadPlan
{
    public required ThreadFormKind Form { get; init; }

    public required string Designation { get; init; }

    public required double MajorMm { get; init; }

    public required double PitchMm { get; init; }

    public required double LeadMm { get; init; }

    public required bool Internal { get; init; }

    public required double ClearanceMm { get; init; }

    public required bool RightHanded { get; init; }

    public required int Starts { get; init; }

    /// <summary>The face's radius.</summary>
    public required double FaceRadiusMm { get; init; }

    /// <summary>The groove's profile polygon, in (radius, axial offset) pairs.</summary>
    public required IReadOnlyList<ProfilePoint> Section { get; init; }

    /// <summary>+1 when the thread runs from the face's low end up, -1 the other way.</summary>
    public required double Direction { get; init; }

    /// <summary>Where the sweep starts, along the face's axis.</summary>
    public required double SweepStartMm { get; init; }

    /// <summary>Where the sweep ends, along the face's axis.</summary>
    public required double SweepEndMm { get; init; }

    /// <summary>How many turns the sweep makes.</summary>
    public required double Turns { get; init; }

    /// <summary>The groove's root radius.</summary>
    public required double RootRadiusMm { get; init; }

    /// <summary>The tooth's crest radius.</summary>
    public required double CrestRadiusMm { get; init; }

    /// <summary>The outermost radius the sweep reaches (past the surface).</summary>
    public required double OuterRadiusMm { get; init; }

    /// <summary>The innermost radius the sweep reaches.</summary>
    public required double InnerRadiusMm { get; init; }

    /// <summary>How far the profile reaches each way along the axis from its centre.</summary>
    public required double WidthLoMm { get; init; }

    /// <inheritdoc cref="WidthLoMm"/>
    public required double WidthHiMm { get; init; }

    /// <summary>How deep the groove cuts into the material, radially.</summary>
    public required double ThreadDepthMm { get; init; }

    /// <summary>The lead-in cone at the free end, if one is wanted.</summary>
    public TipCone? Tip { get; init; }

    /// <summary>The relief at a shoulder, if one is wanted.</summary>
    public ShoulderRelief? Relief { get; init; }

    /// <summary>Something the user should know but that does not stop the build.</summary>
    public string Warning { get; init; } = string.Empty;

    /// <summary>The volume one turn of the groove takes, in cubic millimetres.</summary>
    public double TurnVolumeMm3 { get; init; }

    /// <summary>The length of the sweep, in millimetres.</summary>
    public double SweepLengthMm => Math.Abs(SweepEndMm - SweepStartMm);
}

/// <summary>
/// Turns a request into a plan: the groove's shape and where it runs.
///
/// The rules follow ISO 3508's runouts and the way a thread ends in practice:
/// at a free end the groove runs a pitch past the end into the air so the thread
/// is at full depth on the face; at a closed end it stops with its section clear
/// of the end so it never cuts into the shoulder; at a shoulder the relief
/// takes the last turn away.
/// </summary>
public static class ThreadPlanBuilder
{
    public static ThreadPlan Build(ThreadPlanOptions o)
    {
        if (o.PitchMm <= 0)
        {
            throw new ThreadPlanException("The pitch must be greater than zero.");
        }

        if (o.MajorMm <= 0)
        {
            throw new ThreadPlanException("The thread's diameter must be greater than zero.");
        }

        if (o.LeadMm < o.PitchMm - 1e-9)
        {
            throw new ThreadPlanException("The lead cannot be shorter than the pitch.");
        }

        if (o.FaceRadiusMm <= 0)
        {
            throw new ThreadPlanException("The face has no radius to thread.");
        }

        if (o.FaceHighMm <= o.FaceLowMm)
        {
            throw new ThreadPlanException("The face has no length to thread.");
        }

        int starts = Math.Max(1, o.Starts);
        double lead = o.LeadMm > 0 ? o.LeadMm : o.PitchMm * starts;

        // ---- the groove's profile ------------------------------------------
        GrooveProfile groove = ThreadForms.Groove(o.Form, o.MajorMm, o.PitchMm, o.Internal);

        // The clearance moves the whole groove: an external thread's teeth get
        // thinner and its root deeper, a hole's get thinner and its root wider.
        double clearance = Math.Max(0.0, o.ClearanceMm);
        if (clearance > 0)
        {
            groove = groove.WithOffset(o.Internal ? clearance : -clearance);
        }

        double p = o.PitchMm;
        double faceR = o.FaceRadiusMm;

        double innerR;
        double outerR;
        if (o.Internal)
        {
            // The groove starts inside the hole wall so it leaves no skin, and runs
            // out to the thread's root (its major diameter).
            innerR = Math.Min(faceR, groove.CrestR) - 0.1 * p;
            outerR = groove.RootR;
        }
        else
        {
            // The groove starts at the thread's root and runs out past the surface.
            innerR = groove.RootR;
            outerR = Math.Max(faceR, o.MajorMm / 2.0) + 0.05 * p;
        }

        if (innerR <= 0)
        {
            throw new ThreadPlanException("The thread's dimensions are out of range.");
        }

        double threadDepth = o.Internal ? outerR - faceR : faceR - innerR;
        if (threadDepth < groove.BasicDepth / 4.0)
        {
            string drill = o.Internal
                ? $"drill {ThreadCatalog.Show(ThreadCatalog.TapDrillMm(new ThreadSize(o.Form, o.MajorMm, p, lead)))} mm for it"
                : $"make the shaft {ThreadCatalog.Show(o.MajorMm)} mm for it";
            throw new ThreadPlanException(
                o.Internal
                    ? $"This {ThreadCatalog.Show(2 * faceR)} mm hole is too wide for a modelled {o.Designation} thread: " +
                      $"the cut would take only {threadDepth:0.00} mm of the thread's {groove.BasicDepth:0.00} mm depth. {drill}."
                    : $"This {ThreadCatalog.Show(2 * faceR)} mm shaft is too thin for a modelled {o.Designation} thread: " +
                      $"the cut would take only {threadDepth:0.00} mm of the thread's {groove.BasicDepth:0.00} mm depth. {drill}.");
        }

        var (lo0, hi0) = groove.Extents(p, innerR);
        var (lo1, hi1) = groove.Extents(p, outerR);

        double rootWidth = groove.Width(p, groove.RootR);
        if (rootWidth <= 0.02 * p)
        {
            throw new ThreadPlanException(
                "The clearance is too large for this pitch: the groove would close up at its root.");
        }

        List<ProfilePoint> section = o.Internal
            ? new List<ProfilePoint>
            {
                new(outerR, -lo1), new(innerR, -lo0), new(innerR, hi0), new(outerR, hi1)
            }
            : new List<ProfilePoint>
            {
                new(innerR, -lo0), new(outerR, -lo1), new(outerR, hi1), new(innerR, hi0)
            };

        double widthLo = Math.Max(lo0, lo1);
        double widthHi = Math.Max(hi0, hi1);

        // ---- where the thread runs -----------------------------------------
        double direction = o.StartSide == 1 ? -1.0 : 1.0;
        double faceRun = o.FaceHighMm - o.FaceLowMm;
        double length = o.LengthMm > 0 ? Math.Min(o.LengthMm, faceRun) : faceRun;

        ThreadEndKind startKind = o.StartSide == 1 ? o.HighEndKind : o.LowEndKind;
        ThreadEndKind endKind = o.StartSide == 1 ? o.LowEndKind : o.HighEndKind;

        // The sweep's start: a pitch before a free end, so the groove is at full
        // depth where the face ends and its lead-in lies in the air.
        double s0 = startKind == ThreadEndKind.Free ? -p : 0.0;

        // The sweep's end: a pitch past a free end, or clear of a closed one.
        double s1;
        switch (endKind)
        {
            case ThreadEndKind.Free:
                s1 = length + p;
                break;

            case ThreadEndKind.Inside:
                s1 = length;
                break;

            case ThreadEndKind.Closed:
                // A tap needs room to run out, so the bottom keeps a plain band
                // one pitch deep: the groove's last flank stops a whole pitch
                // above the bottom, not flush with it.
                s1 = Math.Min(length, faceRun - p - widthHi);
                break;

            default:
                s1 = Math.Min(length, faceRun - widthHi);
                break;
        }

        if (s1 - s0 <= p * 1e-3)
        {
            throw new ThreadPlanException(
                "The thread is too short to model: its groove needs " +
                $"{Math.Max(widthLo, widthHi):0.00} mm clear of each end.");
        }

        double turns = (s1 - s0) / lead;
        if (turns > 400.0)
        {
            throw new ThreadPlanException(
                $"A modelled thread is limited to 400 turns ({400 * lead:0} mm at this lead). Use a shorter thread.");
        }

        // ---- the end treatments ---------------------------------------------
        TipCone? tip = null;
        if (o.ChamferStart && startKind == ThreadEndKind.Free)
        {
            // ISO 4753: the end's diameter is bounded by the thread's minor
            // diameter, so the cone crosses the groove's root and leaves the end
            // whole. A cone ending on the root would pinch to nothing.
            double coneDepth = o.Internal
                ? Math.Max(0.6134 * p, threadDepth + 0.08 * p)
                : Math.Max(0.6134 * p, threadDepth + 0.08 * p);
            tip = new TipCone(
                o.Internal ? faceR : faceR - coneDepth,
                coneDepth,
                o.Internal);
        }

        ShoulderRelief? relief = null;
        if (o.ChamferEnd && endKind == ThreadEndKind.Shoulder)
        {
            // As deep as the thread's cut plus the clearance, on the thread's side
            // of the shoulder. A shaft's relief is one pitch high; a hole's is a
            // pitch and a half, because a tap needs more room to run out than a
            // die does.
            double width = o.Internal ? 1.5 * p : p;
            double r0 = o.Internal ? faceR : groove.RootR;
            double r1 = o.Internal ? groove.RootR : faceR;

            relief = new ShoulderRelief(faceRun - width, faceRun, Math.Min(r0, r1), Math.Max(r0, r1));
        }

        // ---- back to the face's own frame ------------------------------------
        double startZ = o.StartSide == 1 ? o.FaceHighMm : o.FaceLowMm;
        double sweepStart = startZ + direction * s0;
        double sweepEnd = startZ + direction * s1;

        // The volume one turn of the groove takes out of the material: the part
        // of the sweep that lies inside it.
        double volumeInner = o.Internal ? Math.Max(innerR, faceR) : innerR;
        double volumeOuter = o.Internal ? outerR : Math.Min(outerR, faceR);
        double turnVolume = groove.TurnVolume(p, volumeInner, volumeOuter);

        string warning = string.Empty;
        if (o.Internal)
        {
            double drill = ThreadCatalog.TapDrillMm(new ThreadSize(o.Form, o.MajorMm, p, lead));
            if (faceR * 2.0 > drill + 0.05)
            {
                warning =
                    $"The hole is {faceR * 2.0:0.###} mm wide, so the thread's crest is cut away and " +
                    $"its minor diameter is the hole's own. Drill to {drill:0.###} mm for the standard thread.";
            }
        }

        return new ThreadPlan
        {
            Form = o.Form,
            Designation = o.Designation,
            MajorMm = o.MajorMm,
            PitchMm = p,
            LeadMm = lead,
            Internal = o.Internal,
            ClearanceMm = clearance,
            RightHanded = o.RightHanded,
            Starts = starts,
            FaceRadiusMm = faceR,
            Section = section,
            Direction = direction,
            SweepStartMm = sweepStart,
            SweepEndMm = sweepEnd,
            Turns = turns,
            RootRadiusMm = groove.RootR,
            CrestRadiusMm = groove.CrestR,
            OuterRadiusMm = outerR,
            InnerRadiusMm = innerR,
            WidthLoMm = widthLo,
            WidthHiMm = widthHi,
            ThreadDepthMm = threadDepth,
            Tip = tip,
            Relief = relief,
            Warning = warning,
            TurnVolumeMm3 = turnVolume
        };
    }
}
