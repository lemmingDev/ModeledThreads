namespace ModeledThreads.Core;

/// <summary>
/// The thread forms (standards) the add-in can cut. Every form is reduced to the
/// same handful of numbers: the pitch, the flank angles, the flat of the tooth's
/// crest and the radius of the groove's root.
/// </summary>
public enum ThreadFormKind
{
    /// <summary>ISO metric, 60° included angle (ISO 68-1 profile, ISO 261/262 sizes).</summary>
    Metric,

    /// <summary>ISO metric trapezoidal, 30° (ISO 2904 profile, ISO 2902 sizes).</summary>
    Trapezoidal,

    /// <summary>General purpose Acme, 29° (ASME B1.5).</summary>
    Acme,

    /// <summary>Metric buttress, 3° load flank / 30° clearance flank (DIN 513-1/-2).</summary>
    Buttress,

    /// <summary>Buttress inch, 7° load flank / 45° clearance flank (ASME B1.9).</summary>
    ButtressAsme,

    /// <summary>Parallel pipe thread, 55° (ISO 228-1, the Whitworth form).</summary>
    Bspp,

    /// <summary>Parallel internal pipe thread, 55° (ISO 7-1's Rp).</summary>
    Rp
}

/// <summary>
/// The space one turn's teeth leave between them, drawn in an axial half-plane:
/// the radius from the axis across, the axial position along the thread.
///
/// A groove is a polygon with straight flanks and a flat crest and root. The
/// crest and root radii of the real standards are not modelled: every groove is
/// the polygon the sweep carries exactly.
/// </summary>
public readonly struct GrooveProfile
{
    /// <summary>The -z wall's angle from the radial direction, in radians.</summary>
    public readonly double FlankLo;

    /// <summary>The +z wall's angle from the radial direction, in radians.</summary>
    public readonly double FlankHi;

    /// <summary>The radius the tooth's crest sits at (the major radius externally).</summary>
    public readonly double CrestR;

    /// <summary>The width of the tooth's crest flat, along the axis.</summary>
    public readonly double CrestFlat;

    /// <summary>The radius of the groove's root (the minor radius externally).</summary>
    public readonly double RootR;

    /// <summary>The basic depth of engagement.</summary>
    public readonly double BasicDepth;

    /// <summary>True for a nut's groove, whose teeth point at the axis.</summary>
    public readonly bool Internal;

    public GrooveProfile(
        double flankLo,
        double flankHi,
        double crestR,
        double crestFlat,
        double rootR,
        double basicDepth,
        bool @internal)
    {
        FlankLo = flankLo;
        FlankHi = flankHi;
        CrestR = crestR;
        CrestFlat = crestFlat;
        RootR = rootR;
        BasicDepth = basicDepth;
        Internal = @internal;
    }

    /// <summary>The groove's axial width at radius <paramref name="r"/>.</summary>
    public double Width(double pitch, double r)
    {
        // How far r is from the crest towards the root (negative past the crest,
        // in the air the groove reaches into).
        double into = Internal ? r - CrestR : CrestR - r;
        return pitch - CrestFlat - into * (Math.Tan(FlankLo) + Math.Tan(FlankHi));
    }

    /// <summary>
    /// The groove's (-z, +z) extents at radius <paramref name="r"/>, its root flat
    /// centred on z = 0.
    /// </summary>
    public (double Lo, double Hi) Extents(double pitch, double r)
    {
        double root = Width(pitch, RootR) / 2.0;
        double d = Math.Abs(r - RootR);
        return (root + d * Math.Tan(FlankLo), root + d * Math.Tan(FlankHi));
    }

    /// <summary>
    /// The volume of one turn of the groove between the radii a and b:
    /// 2 pi integral of r w(r) dr, w linear in r.
    /// </summary>
    public double TurnVolume(double pitch, double a, double b)
    {
        if (Math.Abs(b - a) < 1e-15)
        {
            return 0.0;
        }

        double w0 = Width(pitch, a);
        double w1 = Width(pitch, b);
        double c1 = (w1 - w0) / (b - a);
        double c0 = w0 - c1 * a;

        double F(double r) => c0 * r * r / 2.0 + c1 * r * r * r / 3.0;
        return 2.0 * Math.PI * (F(b) - F(a));
    }

    /// <summary>This profile with its root moved by <paramref name="delta"/>.</summary>
    public GrooveProfile WithRoot(double delta) => new(
        FlankLo, FlankHi, CrestR, CrestFlat, RootR + delta, BasicDepth, Internal);

    /// <summary>
    /// This profile moved bodily towards or away from the axis. The whole groove
    /// goes with it, so the tooth gets thinner (or thicker) without the root's
    /// flat changing: that is what a clearance is for.
    /// </summary>
    public GrooveProfile WithOffset(double delta) => new(
        FlankLo, FlankHi, CrestR + delta, CrestFlat, RootR + delta, BasicDepth, Internal);
}

/// <summary>
/// The profiles of the standards. Each form's basic profile is fixed by its
/// standard; the sizes and designations come from <see cref="ThreadCatalog"/>.
/// </summary>
public static class ThreadForms
{
    public const double Inch = 25.4;

    public static string DisplayName(ThreadFormKind form) => form switch
    {
        ThreadFormKind.Metric => "ISO Metric",
        ThreadFormKind.Trapezoidal => "ISO Trapezoidal",
        ThreadFormKind.Acme => "Acme",
        ThreadFormKind.Buttress => "Metric Buttress",
        ThreadFormKind.ButtressAsme => "Buttress (inch)",
        ThreadFormKind.Bspp => "BSP Parallel (G)",
        ThreadFormKind.Rp => "BSP Parallel Internal (Rp)",
        _ => form.ToString()
    };

    public static string Standard(ThreadFormKind form) => form switch
    {
        ThreadFormKind.Metric => "ISO 68-1, ISO 261",
        ThreadFormKind.Trapezoidal => "ISO 2904, ISO 2902",
        ThreadFormKind.Acme => "ASME B1.5",
        ThreadFormKind.Buttress => "DIN 513-1, DIN 513-2",
        ThreadFormKind.ButtressAsme => "ASME B1.9",
        ThreadFormKind.Bspp => "ISO 228-1",
        ThreadFormKind.Rp => "ISO 7-1",
        _ => string.Empty
    };

    /// <summary>
    /// The groove of the given size: external (the space between a screw's teeth)
    /// or internal (a nut's).
    /// </summary>
    public static GrooveProfile Groove(ThreadFormKind form, double d, double p, bool @internal)
    {
        double rad(double deg) => deg * Math.PI / 180.0;
        (double, double) half(double a) => (rad(a), rad(a));

        // (flanks, the tooth's crest flat, the basic depth, the external minor
        // radius, the internal minor radius, the internal major radius)
        (double, double) flanks;
        double flat;
        double depth;
        double r3;
        double r1;
        double r4;

        switch (form)
        {
            case ThreadFormKind.Metric:
            {
                // ISO 68-1: H = P sqrt(3)/2, the basic depth 5H/8, the flats P/8
                // at the major diameter and P/4 at the minor one.
                double h = p * Math.Sqrt(3.0) / 2.0;
                r3 = d / 2.0 - 5.0 * h / 8.0;
                r1 = r3;
                r4 = d / 2.0;
                flat = @internal ? p / 4.0 : p / 8.0;
                depth = 5.0 * h / 8.0;
                flanks = half(30.0);
                break;
            }

            case ThreadFormKind.Trapezoidal:
            {
                // ISO 2904: 30°, H1 = P/2, the flats P/2 (1 - tan 15°) = 0.366 P at
                // both crests, with the clearance ac on the external minor and the
                // internal major diameters.
                double ac = p <= 1.5 ? 0.15 : p <= 5.0 ? 0.25 : p <= 12.0 ? 0.5 : 1.0;
                flat = p / 2.0 - p / 2.0 * Math.Tan(rad(15.0));
                depth = p / 2.0;
                r3 = d / 2.0 - p / 2.0 - ac;
                r1 = d / 2.0 - p / 2.0;
                r4 = d / 2.0 + ac;
                flanks = half(15.0);
                break;
            }

            case ThreadFormKind.Acme:
            {
                // ASME B1.5 general purpose: 29°, h = P/2, the flat 0.3707 P, the
                // external minor 0.020" under the basic minor, the internal major
                // that far over the basic major.
                double a = p >= Inch / 10.0 - 1e-9 ? 0.020 * Inch : 0.010 * Inch;
                flat = p / 2.0 - p / 2.0 * Math.Tan(rad(14.5));
                depth = p / 2.0;
                r3 = d / 2.0 - p / 2.0 - a / 2.0;
                r1 = d / 2.0 - p / 2.0;
                r4 = d / 2.0 + a / 2.0;
                flanks = half(14.5);
                break;
            }

            case ThreadFormKind.Buttress:
            {
                // DIN 513-1: the 3° load flank, the 30° clearance flank, H1 = 0.75 P,
                // the flat 0.26384 P, the external minor h3 = 0.86777 P deep and the
                // internal thread from D1 = d - 1.5 P to D = d.
                double t3 = Math.Tan(rad(3.0));
                double t30 = Math.Tan(rad(30.0));
                flat = (p - 0.75 * p * (t3 + t30)) / 2.0;
                depth = 0.75 * p;
                r3 = d / 2.0 - 0.86777 * p;
                r1 = d / 2.0 - 0.75 * p;
                r4 = d / 2.0;
                flanks = (rad(3.0), rad(30.0));
                break;
            }

            case ThreadFormKind.ButtressAsme:
            {
                // ASME B1.9 basic profile: the 7° load flank, the 45° clearance
                // flank, h = 0.6 P and the flat 0.16316 P.
                flat = (p - 0.6 * p * (Math.Tan(rad(7.0)) + Math.Tan(rad(45.0)))) / 2.0;
                depth = 0.6 * p;
                r3 = d / 2.0 - 0.6 * p;
                r1 = r3;
                r4 = d / 2.0;
                flanks = (rad(7.0), rad(45.0));
                break;
            }

            case ThreadFormKind.Bspp:
            case ThreadFormKind.Rp:
            {
                // ISO 228-1 / ISO 7-1: 55°, h = 0.640327 P of the triangle
                // H = 0.960491 P, rounded equally at the crest and the root and
                // modelled as flats at the basic height.
                double bigH = 0.960491 * p;
                double h = 0.640327 * p;
                flat = (bigH - h) * Math.Tan(rad(27.5));
                depth = h;
                r3 = d / 2.0 - h;
                r1 = r3;
                r4 = d / 2.0;
                flanks = half(27.5);
                break;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(form));
        }

        // The steep flank faces +z on the screw's teeth: the external groove's -z
        // wall, the internal groove's +z wall (a nut's teeth face the other way).
        double lo = @internal ? flanks.Item2 : flanks.Item1;
        double hi = @internal ? flanks.Item1 : flanks.Item2;

        return @internal
            ? new GrooveProfile(lo, hi, r1, flat, r4, depth, true)
            : new GrooveProfile(lo, hi, d / 2.0, flat, r3, depth, false);
    }

    /// <summary>
    /// The form a designation's prefix names: "M" metric, "Tr" trapezoidal, "S"
    /// buttress, an inch size with "ACME" or "BUTT", "G" or "Rp" a pipe thread.
    /// </summary>
    public static ThreadFormKind? OfDesignation(string designation)
    {
        string t = designation.Trim().ToUpperInvariant().Replace('×', 'X').Replace(" ", string.Empty);
        bool startsWithSize(string prefix) =>
            t.StartsWith(prefix, StringComparison.Ordinal) &&
            t.Length > prefix.Length &&
            char.IsDigit(t[prefix.Length]);

        if (t.Contains("ACME")) return ThreadFormKind.Acme;
        if (t.Contains("BUTT")) return ThreadFormKind.ButtressAsme;
        if (t.StartsWith("TR", StringComparison.Ordinal)) return ThreadFormKind.Trapezoidal;
        if (startsWithSize("RP")) return ThreadFormKind.Rp;
        if (startsWithSize("G")) return ThreadFormKind.Bspp;
        if (t.StartsWith("S", StringComparison.Ordinal)) return ThreadFormKind.Buttress;
        if (t.StartsWith("M", StringComparison.Ordinal)) return ThreadFormKind.Metric;
        return null;
    }
}
