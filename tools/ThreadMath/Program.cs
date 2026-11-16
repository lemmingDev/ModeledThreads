using ModeledThreads.Core;

// A console check of the thread maths, runnable without Inventor. It prints the
// numbers the add-in works from so they can be compared with the standards and
// with another kernel.

int failures = 0;

void Check(string what, bool ok, string detail = "")
{
    Console.WriteLine($"{(ok ? "ok  " : "FAIL")}  {what}{(detail.Length > 0 ? "  " + detail : "")}");
    if (!ok) failures++;
}

void Near(string what, double got, double want, double tol = 1e-6)
{
    Check(what, Math.Abs(got - want) <= tol, $"got {got:0.######}, want {want:0.######}");
}

Console.WriteLine("== designations ==");
foreach (string d in new[]
{
    "M10", "M10x1", "M8x0.75", "M16xPh3P1.5", "Tr20x4", "Tr40x14(P7)",
    "S20x4", "1/2-10 ACME", "1 1/4-5 ACME", "G 1/2", "Rp 1/2"
})
{
    if (ThreadCatalog.TryParse(d, out ThreadSize s, out string err))
    {
        Console.WriteLine($"      {d,-16} -> {s.Form,-13} d={s.MajorMm:0.###} p={s.PitchMm:0.###} lead={s.LeadMm:0.###}");
    }
    else
    {
        Check($"parse {d}", false, err);
    }
}

Console.WriteLine();
Console.WriteLine("== ISO metric profile against the tables ==");
Near("M10 external minor diameter", 2 * ThreadForms.Groove(ThreadFormKind.Metric, 10, 1.5, false).RootR, 8.376, 5e-4);
Near("M10 external root flat = P/4", ThreadForms.Groove(ThreadFormKind.Metric, 10, 1.5, false).Width(1.5, ThreadForms.Groove(ThreadFormKind.Metric, 10, 1.5, false).RootR), 0.375);
Near("M10 external crest flat = P/8", ThreadForms.Groove(ThreadFormKind.Metric, 10, 1.5, false).CrestFlat, 0.1875);
Near("M10 internal minor diameter", 2 * ThreadForms.Groove(ThreadFormKind.Metric, 10, 1.5, true).CrestR, 8.376, 5e-4);
Near("M10 internal major diameter", 2 * ThreadForms.Groove(ThreadFormKind.Metric, 10, 1.5, true).RootR, 10.0);
Near("Tr20x4 external minor (ISO 2904 d3 max)", 2 * ThreadForms.Groove(ThreadFormKind.Trapezoidal, 20, 4, false).RootR, 15.5, 5e-3);
Near("Tr30x6 external minor (ISO 2904 d3 max)", 2 * ThreadForms.Groove(ThreadFormKind.Trapezoidal, 30, 6, false).RootR, 23.0, 5e-3);
Near("Tr40x7 external minor (ISO 2904 d3 max)", 2 * ThreadForms.Groove(ThreadFormKind.Trapezoidal, 40, 7, false).RootR, 32.0, 5e-3);
Near("Tr20x4 internal minor (ISO 2904 D1)", 2 * ThreadForms.Groove(ThreadFormKind.Trapezoidal, 20, 4, true).CrestR, 16.0, 5e-3);
Near("Tr20x4 internal major (ISO 2904 D4)", 2 * ThreadForms.Groove(ThreadFormKind.Trapezoidal, 20, 4, true).RootR, 20.5, 5e-3);
Near("Acme 1/2 flat", ThreadForms.Groove(ThreadFormKind.Acme, 12.7, 2.54, false).CrestFlat, 0.3707 * 2.54, 1e-3);
Near("DIN 513 buttress flat", ThreadForms.Groove(ThreadFormKind.Buttress, 10, 2, false).CrestFlat, 0.26384 * 2, 1e-5);
Near("ASME B1.9 buttress flat", ThreadForms.Groove(ThreadFormKind.ButtressAsme, 50, 4, false).CrestFlat, 0.16316 * 4, 1e-4);

Console.WriteLine();
Console.WriteLine("== the profile is half a pitch at the pitch line ==");
foreach ((ThreadFormKind form, double d, double p) in new[]
{
    (ThreadFormKind.Metric, 20.0, 2.5), (ThreadFormKind.Trapezoidal, 20.0, 2.5),
    (ThreadFormKind.Acme, 20.0, 2.5)
})
{
    var g = ThreadForms.Groove(form, d, p, false);
    double r = d / 2.0 - (form == ThreadFormKind.Metric ? 3.0 * p * Math.Sqrt(3.0) / 16.0 : p / 4.0);
    Near($"{form} groove width at the pitch line", g.Width(p, r), p / 2.0, 1e-9);
}

Console.WriteLine();
Console.WriteLine("== the section of M10x1.5 ==");
void ShowSection(string title, ThreadPlanOptions o)
{
    Console.WriteLine($"  {title}");
    try
    {
        ThreadPlan plan = ThreadPlanBuilder.Build(o);
        Console.WriteLine($"     sweep  {plan.SweepStartMm:0.###} .. {plan.SweepEndMm:0.###} mm  " +
                          $"({plan.SweepLengthMm:0.###} mm, {plan.Turns:0.##} turns, dir {plan.Direction:+0;-0})");
        Console.WriteLine($"     root r={plan.RootRadiusMm:0.####}  crest r={plan.CrestRadiusMm:0.####}  " +
                          $"band {plan.InnerRadiusMm:0.####} .. {plan.OuterRadiusMm:0.####}  depth {plan.ThreadDepthMm:0.####}");
        Console.WriteLine($"     section (r, z): " + string.Join("  ", plan.Section.Select(p => $"({p.R:0.####},{p.Z:0.####})")));
        Console.WriteLine($"     turn volume {plan.TurnVolumeMm3:0.####} mm3   width {plan.WidthLoMm:0.####} / {plan.WidthHiMm:0.####}");
        if (plan.Tip is { } tip)
        {
            Console.WriteLine($"     tip cone  r={tip.RadiusMm:0.####} depth={tip.DepthMm:0.####} internal={tip.Internal}");
        }

        if (plan.Relief is { } rel)
        {
            Console.WriteLine($"     relief   z {rel.StartMm:0.###} .. {rel.EndMm:0.###}  r {rel.InnerRadiusMm:0.####} .. {rel.OuterRadiusMm:0.####}");
        }

        if (plan.Warning.Length > 0)
        {
            Console.WriteLine($"     warning: {plan.Warning}");
        }
    }
    catch (ThreadPlanException ex)
    {
        Console.WriteLine($"     refused: {ex.Message}");
    }
}

ShowSection("external M10x1.5 on a 10 mm shaft, free at the bottom, shoulder at the top", new ThreadPlanOptions
{
    Form = ThreadFormKind.Metric,
    Designation = "M10x1.5",
    MajorMm = 10,
    PitchMm = 1.5,
    LeadMm = 1.5,
    FaceRadiusMm = 5,
    FaceLowMm = 0,
    FaceHighMm = 20,
    StartSide = 0,
    LowEndKind = ThreadEndKind.Free,
    HighEndKind = ThreadEndKind.Shoulder,
    ChamferStart = true,
    ChamferEnd = true
});

ShowSection("internal M10x1.5 in a 8.5 mm through hole", new ThreadPlanOptions
{
    Form = ThreadFormKind.Metric,
    Designation = "M10x1.5",
    MajorMm = 10,
    PitchMm = 1.5,
    LeadMm = 1.5,
    Internal = true,
    FaceRadiusMm = 4.25,
    FaceLowMm = 0,
    FaceHighMm = 20,
    StartSide = 0,
    LowEndKind = ThreadEndKind.Free,
    HighEndKind = ThreadEndKind.Free,
    ChamferStart = true
});

ShowSection("internal M10x1.5 in a 8.5 mm blind hole 20 deep", new ThreadPlanOptions
{
    Form = ThreadFormKind.Metric,
    Designation = "M10x1.5",
    MajorMm = 10,
    PitchMm = 1.5,
    LeadMm = 1.5,
    Internal = true,
    FaceRadiusMm = 4.25,
    FaceLowMm = 0,
    FaceHighMm = 20,
    StartSide = 0,
    LowEndKind = ThreadEndKind.Closed,
    HighEndKind = ThreadEndKind.Free,
    ChamferStart = true
});

ShowSection("external M10x1.5 with 0.2 mm clearance", new ThreadPlanOptions
{
    Form = ThreadFormKind.Metric,
    Designation = "M10x1.5",
    MajorMm = 10,
    PitchMm = 1.5,
    LeadMm = 1.5,
    ClearanceMm = 0.2,
    FaceRadiusMm = 5,
    FaceLowMm = 0,
    FaceHighMm = 20,
    LowEndKind = ThreadEndKind.Free,
    HighEndKind = ThreadEndKind.Free
});

Console.WriteLine();
Console.WriteLine("== where a thread stops at each kind of end ==");
{
    static ThreadPlanOptions Ends(
        double lowMm, double highMm, ThreadEndKind low, ThreadEndKind high, bool relief) => new()
    {
        Form = ThreadFormKind.Metric,
        Designation = "M10x1.5",
        MajorMm = 10,
        PitchMm = 1.5,
        LeadMm = 1.5,
        Internal = true,
        FaceRadiusMm = 4.25,
        FaceLowMm = lowMm,
        FaceHighMm = highMm,
        StartSide = 0,
        LowEndKind = low,
        HighEndKind = high,
        ChamferEnd = relief
    };

    // A free start: the groove begins a pitch before the face, in the air, so
    // the first turn is at full depth where the face ends.
    ThreadPlan through = ThreadPlanBuilder.Build(
        Ends(0, 20, ThreadEndKind.Free, ThreadEndKind.Free, relief: false));
    Near("a free start runs one pitch into the air", through.SweepStartMm, -1.5);
    Near("a free end runs one pitch past the face", through.SweepEndMm, 21.5);

    // A blind hole's bottom: the last tooth stops a whole pitch above it, so
    // the wall there stays plain.
    ThreadPlan blind = ThreadPlanBuilder.Build(
        Ends(0, 15, ThreadEndKind.Free, ThreadEndKind.Closed, relief: false));
    Near("a closed end leaves the bottom a plain pitch deep",
        blind.SweepEndMm + blind.WidthHiMm, 15.0 - 1.5);
    Check("a closed end is threaded less than the hole is deep",
        blind.SweepLengthMm < 15.0, $"{blind.SweepLengthMm:0.###} mm of 15 mm");

    // A shoulder: the thread runs on to the face, and the relief takes the last
    // turn away. The hole is cut one and a half pitches deep, so a tap has room
    // to run out; a shaft keeps its one pitch.
    ThreadPlan shoulder = ThreadPlanBuilder.Build(
        Ends(0, 15, ThreadEndKind.Free, ThreadEndKind.Shoulder, relief: true));
    Near("a shoulder's thread runs up to the face",
        shoulder.SweepEndMm + shoulder.WidthHiMm, 15.0);
    Check("a shoulder gets a relief", shoulder.Relief is not null);
    if (shoulder.Relief is { } relief)
    {
        Near("the relief is a pitch and a half deep", relief.EndMm - relief.StartMm, 2.25);
        Near("the relief starts a pitch and a half before the shoulder", relief.StartMm, 12.75);
    }

    // A shaft keeps the one-pitch relief: a die does not need the tap's runout.
    ThreadPlan shaft = ThreadPlanBuilder.Build(new ThreadPlanOptions
    {
        Form = ThreadFormKind.Metric,
        Designation = "M10x1.5",
        MajorMm = 10,
        PitchMm = 1.5,
        LeadMm = 1.5,
        Internal = false,
        FaceRadiusMm = 5,
        FaceLowMm = 0,
        FaceHighMm = 15,
        StartSide = 0,
        LowEndKind = ThreadEndKind.Free,
        HighEndKind = ThreadEndKind.Shoulder,
        ChamferEnd = true
    });

    Check("a shaft's shoulder gets a relief", shaft.Relief is not null);
    if (shaft.Relief is { } shaftRelief)
    {
        Near("a shaft's relief is one pitch deep", shaftRelief.EndMm - shaftRelief.StartMm, 1.5);
        Near("a shaft's relief starts one pitch before the shoulder", shaftRelief.StartMm, 13.5);
    }
}

Console.WriteLine();
Console.WriteLine("== every size in every list reads back as itself ==");
foreach (ThreadFormKind form in (ThreadFormKind[])Enum.GetValues(typeof(ThreadFormKind)))
{
    int count = 0;
    int wrong = 0;
    string first = string.Empty;

    foreach (string designation in ThreadCatalog.Designations(form))
    {
        count++;

        if (!ThreadCatalog.TryParse(designation, out ThreadSize size, out string error))
        {
            wrong++;
            if (first.Length == 0) { first = $"{designation} does not parse: {error}"; }
            continue;
        }

        string written = ThreadCatalog.Format(size);
        if (!ThreadCatalog.TryParse(written, out ThreadSize again, out error)
            || Math.Abs(again.MajorMm - size.MajorMm) > 1e-9
            || Math.Abs(again.PitchMm - size.PitchMm) > 1e-9)
        {
            wrong++;
            if (first.Length == 0) { first = $"{designation} is written as '{written}', which is not the same size"; }
        }
    }

    Check($"{form}: all {count} sizes read back", wrong == 0, first);
}

Console.WriteLine();
Console.WriteLine("== the size the dialog suggests ==");
{
    ThreadSize? fit = ThreadCatalog.Fit(ThreadFormKind.Metric, 10.0, false);
    Check("a 10 mm shaft fits an M10", fit is not null && Math.Abs(fit.Value.MajorMm - 10.0) < 1e-9,
        fit is null ? "no fit at all" : ThreadCatalog.Format(fit.Value));

    if (fit is not null)
    {
        string text = ThreadCatalog.Format(fit.Value);
        bool inList = false;
        int count = 0;
        foreach (string d in ThreadCatalog.Designations(ThreadFormKind.Metric))
        {
            count++;
            if (d == text)
            {
                inList = true;
            }
        }

        Check($"'{text}' is one of the {count} metric sizes", inList,
            inList ? string.Empty : "so the dialog cannot pick it from the list");
    }
}

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
return failures == 0 ? 0 : 1;
