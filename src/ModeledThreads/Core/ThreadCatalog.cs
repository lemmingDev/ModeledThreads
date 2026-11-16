using System.Globalization;

namespace ModeledThreads.Core;

/// <summary>A thread's size: its nominal (major) diameter, its pitch and its lead, in mm.</summary>
public readonly record struct ThreadSize(
    ThreadFormKind Form,
    double MajorMm,
    double PitchMm,
    double LeadMm)
{
    public double TurnsPerUnit => PitchMm > 0 ? LeadMm / PitchMm : 0;
}

/// <summary>
/// The sizes the standards list and the designations that name them. Nothing
/// here comes from Inventor: the add-in carries its own tables, so a thread can
/// be created on a face without a cosmetic thread feature existing first.
/// </summary>
public static class ThreadCatalog
{
    /// <summary>ISO 261/262 coarse pitches: (nominal diameter, pitch).</summary>
    private static readonly (double D, double P)[] MetricCoarse =
    {
        (1.0, 0.25), (1.2, 0.25), (1.6, 0.35), (2.0, 0.4), (2.5, 0.45),
        (3.0, 0.5), (3.5, 0.6), (4.0, 0.7), (4.5, 0.75), (5.0, 0.8),
        (5.5, 0.9), (6.0, 1.0), (7.0, 1.0), (8.0, 1.25), (9.0, 1.25),
        (10.0, 1.5), (11.0, 1.5), (12.0, 1.75), (14.0, 2.0), (16.0, 2.0),
        (18.0, 2.5), (20.0, 2.5), (22.0, 2.5), (24.0, 3.0), (27.0, 3.0),
        (30.0, 3.5), (33.0, 3.5), (36.0, 4.0), (39.0, 4.0), (42.0, 4.5),
        (45.0, 4.5), (48.0, 5.0), (52.0, 5.0), (56.0, 5.5), (60.0, 5.5),
        (64.0, 6.0)
    };

    /// <summary>ISO 261 fine pitches: diameter -> its fine pitch series.</summary>
    private static readonly Dictionary<double, double[]> MetricFine = new()
    {
        [1.6] = new[] { 0.35 }, [2.0] = new[] { 0.4 }, [2.5] = new[] { 0.45 },
        [3.0] = new[] { 0.5 }, [3.5] = new[] { 0.6 }, [4.0] = new[] { 0.7 },
        [4.5] = new[] { 0.75 }, [5.0] = new[] { 0.8 }, [5.5] = new[] { 0.9 },
        [6.0] = new[] { 1.0 }, [7.0] = new[] { 1.0 }, [8.0] = new[] { 1.0, 1.25 },
        [9.0] = new[] { 1.0, 1.25 }, [10.0] = new[] { 1.0, 1.25 },
        [11.0] = new[] { 1.0, 1.25 }, [12.0] = new[] { 1.0, 1.25, 1.5, 1.75 },
        [14.0] = new[] { 1.0, 1.5, 2.0 }, [16.0] = new[] { 1.0, 1.5, 2.0 },
        [18.0] = new[] { 1.0, 1.5, 2.0, 2.5 }, [20.0] = new[] { 1.0, 1.5, 2.0, 2.5 },
        [22.0] = new[] { 1.0, 1.5, 2.0, 2.5 }, [24.0] = new[] { 1.0, 1.5, 2.0, 2.5, 3.0 },
        [27.0] = new[] { 1.0, 1.5, 2.0, 2.5, 3.0 }, [30.0] = new[] { 1.0, 1.5, 2.0, 3.0, 3.5 },
        [33.0] = new[] { 1.0, 1.5, 2.0, 3.0, 3.5 }, [35.0] = new[] { 1.5, 2.0, 3.0, 3.5 },
        [36.0] = new[] { 1.0, 1.5, 2.0, 3.0, 4.0 }, [39.0] = new[] { 1.5, 2.0, 3.0, 4.0 },
        [40.0] = new[] { 1.5, 2.0, 3.0, 4.0 }, [42.0] = new[] { 1.5, 2.0, 3.0, 4.0, 4.5 },
        [45.0] = new[] { 1.5, 2.0, 3.0, 4.0, 4.5 }, [48.0] = new[] { 1.0, 1.5, 2.0, 3.0, 4.0, 5.0 },
        [52.0] = new[] { 1.5, 2.0, 3.0, 4.0, 5.0 }, [56.0] = new[] { 1.5, 2.0, 3.0, 4.0, 5.0, 5.5 },
        [60.0] = new[] { 1.5, 2.0, 3.0, 4.0, 5.0, 5.5 }, [64.0] = new[] { 2.0, 3.0, 4.0, 5.0, 6.0 }
    };

    /// <summary>ISO 2902's middle pitch per diameter, shared by Tr and the metric buttress.</summary>
    private static readonly (double D, double P)[] TrapezoidalSizes =
    {
        (8.0, 1.5), (9.0, 2.0), (10.0, 2.0), (11.0, 2.0), (12.0, 3.0),
        (14.0, 3.0), (16.0, 4.0), (18.0, 4.0), (20.0, 4.0), (22.0, 5.0),
        (24.0, 5.0), (26.0, 5.0), (28.0, 5.0), (30.0, 6.0), (32.0, 6.0),
        (34.0, 6.0), (36.0, 6.0), (38.0, 7.0), (40.0, 7.0), (42.0, 7.0),
        (44.0, 7.0), (46.0, 8.0), (48.0, 8.0), (50.0, 8.0), (52.0, 8.0),
        (55.0, 9.0), (60.0, 9.0), (65.0, 10.0), (70.0, 10.0), (75.0, 10.0),
        (80.0, 10.0), (85.0, 12.0), (90.0, 12.0), (95.0, 12.0), (100.0, 12.0),
        (110.0, 12.0), (120.0, 14.0)
    };

    /// <summary>ASME B1.5 general purpose Acme: (name, inches, threads per inch).</summary>
    private static readonly (string Name, double D, double Tpi)[] AcmeSizes =
    {
        ("1/4", 0.25, 16.0), ("5/16", 0.3125, 14.0), ("3/8", 0.375, 12.0),
        ("7/16", 0.4375, 12.0), ("1/2", 0.5, 10.0), ("5/8", 0.625, 8.0),
        ("3/4", 0.75, 6.0), ("7/8", 0.875, 6.0), ("1", 1.0, 5.0),
        ("1 1/8", 1.125, 5.0), ("1 1/4", 1.25, 5.0), ("1 3/8", 1.375, 4.0),
        ("1 1/2", 1.5, 4.0), ("1 3/4", 1.75, 4.0), ("2", 2.0, 4.0),
        ("2 1/4", 2.25, 3.0), ("2 1/2", 2.5, 3.0), ("2 3/4", 2.75, 3.0),
        ("3", 3.0, 2.0), ("3 1/2", 3.5, 2.0), ("4", 4.0, 2.0),
        ("4 1/2", 4.5, 2.0), ("5", 5.0, 2.0)
    };

    /// <summary>ISO 228-1's G sizes, which are ISO 7-1's gauge diameters: (name, mm, tpi).</summary>
    private static readonly (string Name, double D, double Tpi)[] PipeSizes =
    {
        ("1/16", 7.721, 28.0), ("1/8", 9.721, 28.0), ("1/4", 13.157, 19.0),
        ("3/8", 16.662, 19.0), ("1/2", 20.955, 14.0), ("5/8", 23.441, 14.0),
        ("3/4", 26.441, 14.0), ("7/8", 29.441, 14.0), ("1", 33.249, 11.0),
        ("1 1/8", 37.157, 11.0), ("1 1/4", 41.910, 11.0), ("1 3/8", 45.917, 11.0),
        ("1 1/2", 50.006, 10.0), ("1 3/4", 58.334, 10.0), ("2", 65.710, 10.0),
        ("2 1/4", 73.037, 8.0), ("2 1/2", 77.927, 8.0), ("2 3/4", 86.631, 8.0),
        ("3", 91.925, 8.0), ("3 1/4", 100.076, 6.0), ("3 1/2", 104.633, 6.0),
        ("4", 115.925, 6.0), ("4 1/2", 127.216, 6.0), ("5", 138.507, 5.0),
        ("5 1/2", 149.799, 5.0), ("6", 161.090, 5.0)
    };

    /// <summary>The tap maker's drill for the metric coarse pitches (ISO 5290 / DIN 13).</summary>
    private static readonly Dictionary<double, double> MetricDrills = new()
    {
        [1.6] = 1.25, [2.0] = 1.6, [2.5] = 2.05, [3.0] = 2.5, [3.5] = 2.9,
        [4.0] = 3.3, [4.5] = 3.8, [5.0] = 4.2, [5.5] = 4.6, [6.0] = 5.0,
        [7.0] = 6.0, [8.0] = 6.8, [9.0] = 7.9, [10.0] = 8.5, [11.0] = 9.5,
        [12.0] = 10.2, [14.0] = 12.0, [16.0] = 14.0, [18.0] = 15.5,
        [20.0] = 17.5, [22.0] = 19.5, [24.0] = 21.0, [27.0] = 24.0,
        [30.0] = 26.5, [33.0] = 29.0, [36.0] = 31.5, [39.0] = 34.5,
        [42.0] = 37.5, [45.0] = 40.5, [48.0] = 43.5, [52.0] = 47.0,
        [56.0] = 50.0, [60.0] = 53.0, [64.0] = 56.0
    };

    /// <summary>The designations a form's standard writes, coarse first then the fine pitches.</summary>
    public static IEnumerable<string> Designations(ThreadFormKind form)
    {
        switch (form)
        {
            case ThreadFormKind.Metric:
                // The dialog shows the pitch with every size, so a coarse M10
                // appears as M10x1.5 rather than a bare M10. Typing M10 still
                // parses; this is only what the list and the detected size show.
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (d, p) in MetricCoarse)
                {
                    string designation = FormatMetric(d, p, p);
                    if (seen.Add(designation))
                    {
                        yield return designation;
                    }
                }

                foreach (var (d, _) in MetricCoarse)
                {
                    if (!MetricFine.TryGetValue(d, out double[]? fines))
                    {
                        continue;
                    }

                    foreach (double p in fines)
                    {
                        string designation = FormatMetric(d, p, p);
                        if (seen.Add(designation))
                        {
                            yield return designation;
                        }
                    }
                }

                break;

            case ThreadFormKind.Trapezoidal:
                foreach (var (d, p) in TrapezoidalSizes)
                {
                    yield return $"Tr{Show(d)}x{Show(p)}";
                }

                break;

            case ThreadFormKind.Buttress:
                foreach (var (d, p) in TrapezoidalSizes.Where(s => s.D >= 10.0))
                {
                    yield return $"S{Show(d)}x{Show(p)}";
                }

                break;

            case ThreadFormKind.Acme:
                foreach (var (name, _, tpi) in AcmeSizes)
                {
                    yield return $"{name}-{Show(tpi)} ACME";
                }

                break;

            case ThreadFormKind.ButtressAsme:
                yield break;

            case ThreadFormKind.Bspp:
                foreach (var (name, _, _) in PipeSizes)
                {
                    yield return $"G {name}";
                }

                break;

            case ThreadFormKind.Rp:
                foreach (var (name, _, _) in PipeSizes)
                {
                    yield return $"Rp {name}";
                }

                break;
        }
    }

    /// <summary>The sizes of a form, as (diameter, pitch) pairs.</summary>
    public static IEnumerable<(double D, double P)> Sizes(ThreadFormKind form)
    {
        switch (form)
        {
            case ThreadFormKind.Metric:
                foreach (var (d, p) in MetricCoarse)
                {
                    yield return (d, p);
                }

                foreach (var (d, _) in MetricCoarse)
                {
                    if (!MetricFine.TryGetValue(d, out double[]? fines))
                    {
                        continue;
                    }

                    foreach (double p in fines)
                    {
                        yield return (d, p);
                    }
                }

                break;

            case ThreadFormKind.Trapezoidal:
                foreach (var s in TrapezoidalSizes)
                {
                    yield return s;
                }

                break;

            case ThreadFormKind.Buttress:
                foreach (var s in TrapezoidalSizes.Where(s => s.D >= 10.0))
                {
                    yield return s;
                }

                break;

            case ThreadFormKind.Acme:
                foreach (var (_, d, tpi) in AcmeSizes)
                {
                    yield return (d * ThreadForms.Inch, ThreadForms.Inch / tpi);
                }

                break;

            case ThreadFormKind.Bspp:
            case ThreadFormKind.Rp:
                foreach (var (_, d, tpi) in PipeSizes)
                {
                    yield return (d, ThreadForms.Inch / tpi);
                }

                break;

            default:
                yield break;
        }
    }

    /// <summary>
    /// Reads a designation: "M10", "M10x1", "M16xPh3P1.5", "Tr20x4", "Tr40x14(P7)",
    /// "S20x4", "1/2-10 ACME", "G 1/2", "Rp 1/2".
    /// </summary>
    public static bool TryParse(string designation, out ThreadSize size, out string error)
    {
        size = default;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(designation))
        {
            error = "No designation given.";
            return false;
        }

        string raw = designation.Trim();
        string t = raw.ToUpperInvariant().Replace('×', 'X').Replace(" ", string.Empty);

        ThreadFormKind? form = ThreadForms.OfDesignation(raw);
        if (form is null)
        {
            error = $"'{raw}' does not name a thread form. Use M10, M10x1, Tr20x4, 1/2-10 ACME, G 1/2 or Rp 1/2.";
            return false;
        }

        switch (form.Value)
        {
            case ThreadFormKind.Metric:
            {
                string body = t[1..];
                if (!TrySplitSize(body, out double d, out double? pitch, out double? lead, out error))
                {
                    return false;
                }

                double? standard = CoarsePitch(ThreadFormKind.Metric, d);
                double p = pitch ?? standard ?? 0.0;
                if (pitch is null && standard is null)
                {
                    error = NoCoarsePitch(d);
                    return false;
                }

                size = new ThreadSize(form.Value, d, p, lead ?? p);
                break;
            }

            case ThreadFormKind.Trapezoidal:
            case ThreadFormKind.Buttress:
            {
                string prefix = form.Value == ThreadFormKind.Trapezoidal ? "TR" : "S";
                string body = t[prefix.Length..];
                if (!TrySplitSize(body, out double d, out double? pitch, out double? lead, out error))
                {
                    return false;
                }

                double? standard = CoarsePitch(form.Value, d);
                double p = pitch ?? standard ?? 0.0;
                if (pitch is null && standard is null)
                {
                    error = $"No standard pitch for a {Show(d)} mm {ThreadForms.DisplayName(form.Value)} thread. Give one.";
                    return false;
                }

                size = new ThreadSize(form.Value, d, p, lead ?? p);
                break;
            }

            case ThreadFormKind.Acme:
            {
                // Inches keep their spaces here: "1 1/8-5" is one and an eighth
                // inch, which the stripped form "11/8-5" would not say.
                string body = raw;
                foreach (string suffix in new[] { "ACME", "PUSH-BUTT", "PULL-BUTT", "BUTT" })
                {
                    if (body.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        body = body[..^suffix.Length];
                        break;
                    }
                }

                body = body.Trim().TrimEnd('-').Trim();
                string[] parts = body.Split('-');
                if (parts.Length < 2 || !TryNumber(parts[0], out double dIn) || !TryNumber(parts[1], out double tpi))
                {
                    error = $"'{raw}' is not an Acme designation. Use 1/2-10 ACME.";
                    return false;
                }

                size = new ThreadSize(form.Value, dIn * ThreadForms.Inch, ThreadForms.Inch / tpi, ThreadForms.Inch / tpi);
                break;
            }

            case ThreadFormKind.Bspp:
            case ThreadFormKind.Rp:
            {
                string prefix = form.Value == ThreadFormKind.Bspp ? "G" : "RP";
                string body = t[prefix.Length..].TrimEnd('A', 'B');
                var found = PipeSizes.FirstOrDefault(p => p.Name.Replace(" ", string.Empty) == body);
                if (found.Name is null)
                {
                    error = $"'{raw}' is not a pipe thread size. Use G 1/2 or Rp 1/2.";
                    return false;
                }

                double p = ThreadForms.Inch / found.Tpi;
                size = new ThreadSize(form.Value, found.D, p, p);
                break;
            }
        }

        if (!(size.MajorMm > 0 && size.PitchMm > 0 && size.LeadMm > 0))
        {
            error = $"'{raw}' does not give a usable thread size.";
            return false;
        }

        if (size.PitchMm >= size.MajorMm)
        {
            error = $"'{raw}' has a pitch as large as its diameter.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// The drill a hole is tapped with: the tap maker's drill for a metric coarse
    /// pitch, otherwise the internal thread's minor diameter less a little.
    /// </summary>
    public static double TapDrillMm(ThreadSize size)
    {
        if (size.Form == ThreadFormKind.Metric)
        {
            if (MetricDrills.TryGetValue(size.MajorMm, out double drill))
            {
                double? coarse = CoarsePitch(ThreadFormKind.Metric, size.MajorMm);
                if (coarse is null || Math.Abs(coarse.Value - size.PitchMm) < 1e-9)
                {
                    return drill;
                }
            }

            double h = size.PitchMm * Math.Sqrt(3.0) / 2.0;
            return size.MajorMm - 2.0 * (5.0 * h / 8.0) - 0.05;
        }

        var profile = ThreadForms.Groove(size.Form, size.MajorMm, size.PitchMm, true);
        return 2.0 * profile.CrestR;
    }

    /// <summary>
    /// The standard size that fits a face of the given diameter: a shaft is its
    /// major diameter, a hole its drill. None when nothing is close enough.
    /// </summary>
    public static ThreadSize? Fit(ThreadFormKind form, double diameterMm, bool @internal)
    {
        ThreadSize? best = null;
        double bestError = double.MaxValue;

        foreach (var (d, p) in Sizes(form))
        {
            double want = @internal ? TapDrillMm(new ThreadSize(form, d, p, p)) : d;
            double error = Math.Abs(want - diameterMm);
            if (error < bestError)
            {
                bestError = error;
                best = new ThreadSize(form, d, p, p);
            }
        }

        if (best is null || bestError > 0.15 * diameterMm)
        {
            return null;
        }

        return best;
    }

    /// <summary>The standard pitch of a diameter in a form's table.</summary>
    public static double? CoarsePitch(ThreadFormKind form, double d)
    {
        foreach ((double dd, double p) in Sizes(form))
        {
            if (Math.Abs(dd - d) < 1e-9)
            {
                return p;
            }
        }

        return null;
    }

    /// <summary>The designation the standard writes for a size.</summary>
    public static string Format(ThreadSize size)
    {
        bool multi = Math.Abs(size.LeadMm - size.PitchMm) > 1e-9;
        switch (size.Form)
        {
            case ThreadFormKind.Metric:
                if (multi)
                {
                    return $"M{Show(size.MajorMm)}xPh{Show(size.LeadMm)}P{Show(size.PitchMm)}";
                }

                double? coarse = CoarsePitch(ThreadFormKind.Metric, size.MajorMm);
                return coarse is not null && Math.Abs(coarse.Value - size.PitchMm) < 1e-9
                    ? $"M{Show(size.MajorMm)}"
                    : $"M{Show(size.MajorMm)}x{Show(size.PitchMm)}";

            case ThreadFormKind.Trapezoidal:
                return multi
                    ? $"Tr{Show(size.MajorMm)}x{Show(size.LeadMm)}(P{Show(size.PitchMm)})"
                    : $"Tr{Show(size.MajorMm)}x{Show(size.PitchMm)}";

            case ThreadFormKind.Buttress:
                return multi
                    ? $"S{Show(size.MajorMm)}x{Show(size.LeadMm)}(P{Show(size.PitchMm)})"
                    : $"S{Show(size.MajorMm)}x{Show(size.PitchMm)}";

            case ThreadFormKind.Acme:
            {
                double dIn = size.MajorMm / ThreadForms.Inch;
                var named = AcmeSizes.FirstOrDefault(s => Math.Abs(s.D - dIn) < 1e-9);
                string name = named.Name is null ? Show(dIn) : named.Name;
                return $"{name}-{Show(ThreadForms.Inch / size.PitchMm)} ACME";
            }

            default:
                return size.Form == ThreadFormKind.Rp
                    ? $"Rp {PipeSizes.FirstOrDefault(s => Math.Abs(s.D - size.MajorMm) < 1e-6).Name}"
                    : $"G {PipeSizes.FirstOrDefault(s => Math.Abs(s.D - size.MajorMm) < 1e-6).Name}";
        }
    }

    /// <summary>
    /// The designation with the pitch always spelled out, as the dialog shows it
    /// when it detects the size for a face: a coarse M10 is shown as M10x1.5.
    /// Standards whose designation already carries the pitch (Tr, S, ACME) are
    /// unchanged, and a pipe thread keeps its standard name.
    /// </summary>
    public static string FormatWithPitch(ThreadSize size)
    {
        if (size.Form != ThreadFormKind.Metric)
        {
            return Format(size);
        }

        bool multi = Math.Abs(size.LeadMm - size.PitchMm) > 1e-9;
        return multi
            ? $"M{Show(size.MajorMm)}xPh{Show(size.LeadMm)}P{Show(size.PitchMm)}"
            : $"M{Show(size.MajorMm)}x{Show(size.PitchMm)}";
    }

    /// <summary>A number as the standards write it.</summary>
    public static string Show(double v)
    {
        string text = v.ToString("0.####", CultureInfo.InvariantCulture);

        // Only a fraction's trailing zeros go: "1.50" is 1.5, but "10" is ten,
        // not one.
        return text.IndexOf('.') >= 0 ? text.TrimEnd('0').TrimEnd('.') : text;
    }

    private static string FormatMetric(double d, double p, double lead)
    {
        bool multi = Math.Abs(lead - p) > 1e-9;
        return multi
            ? $"M{Show(d)}xPh{Show(lead)}P{Show(p)}"
            : $"M{Show(d)}x{Show(p)}";
    }

    /// <summary>
    /// Splits a size body into its diameter and its optional pitch and lead:
    /// "10", "10x1", "10x1.5", "40x14(P7)", "16xPh3P1.5".
    /// </summary>
    private static bool TrySplitSize(
        string body,
        out double diameter,
        out double? pitch,
        out double? lead,
        out string error)
    {
        diameter = 0;
        pitch = null;
        lead = null;
        error = string.Empty;

        string b = body.Replace(" ", string.Empty);
        int split = b.IndexOf('X');
        if (split < 0)
        {
            if (!TryNumber(b, out diameter))
            {
                error = $"'{b}' is not a diameter.";
                return false;
            }

            return true;
        }

        string dPart = b[..split];
        string rest = b[(split + 1)..];

        if (!TryNumber(dPart, out diameter))
        {
            error = $"'{dPart}' is not a diameter.";
            return false;
        }

        if (rest.StartsWith("PH", StringComparison.OrdinalIgnoreCase))
        {
            // M16xPh3P1.5: the lead 3, the pitch 1.5.
            int pIdx = rest.IndexOf('P', 2);
            if (pIdx < 0 || !TryNumber(rest[2..pIdx], out double l) || !TryNumber(rest[(pIdx + 1)..], out double p))
            {
                error = $"'{body}' is not a multi-start designation. Use M16xPh3P1.5.";
                return false;
            }

            lead = l;
            pitch = p;
            return true;
        }

        int leadOpen = rest.IndexOf('(');
        if (leadOpen > 0 && rest.EndsWith(")", StringComparison.Ordinal))
        {
            // Tr40x14(P7): the lead 14, the pitch 7.
            if (!TryNumber(rest[..leadOpen], out double l) ||
                !TryNumber(rest[(leadOpen + 2)..^1], out double p))
            {
                error = $"'{body}' is not a lead and pitch designation. Use Tr40x14(P7).";
                return false;
            }

            lead = l;
            pitch = p;
            return true;
        }

        if (!TryNumber(rest, out double only))
        {
            error = $"'{rest}' is not a pitch.";
            return false;
        }

        pitch = only;
        return true;
    }

    private static bool TryNumber(string text, out double value)
    {
        text = text.Trim();
        if (text.Length == 0)
        {
            value = 0;
            return false;
        }

        // A mixed number: "1 1/8" is one and an eighth, not eleven eighths.
        int space = text.IndexOf(' ');
        if (space > 0)
        {
            if (TryNumber(text[..space], out double whole) && TryNumber(text[(space + 1)..], out double part))
            {
                value = whole + part;
                return !double.IsNaN(value) && !double.IsInfinity(value);
            }

            value = 0;
            return false;
        }

        int slash = text.IndexOf('/');
        if (slash > 0)
        {
            if (TryNumber(text[..slash], out double num) && TryNumber(text[(slash + 1)..], out double den) && den != 0)
            {
                value = num / den;
                return !double.IsNaN(value) && !double.IsInfinity(value);
            }

            value = 0;
            return false;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static string NoCoarsePitch(double d)
    {
        double[] pitches = { 2.0, 1.5, 1.25, 1.0, 0.75, 0.5, 0.35, 0.25, 0.2 };
        int first = Array.FindIndex(pitches, p => p <= 0.12 * d);
        if (first < 0)
        {
            first = pitches.Length - 1;
        }

        var names = pitches.Skip(first).Take(2).Where(p => p < d).Select(p => $"M{Show(d)}x{Show(p)}").ToList();
        return names.Count == 0
            ? $"M{Show(d)} has no coarse ISO pitch. Give one."
            : $"M{Show(d)} has no coarse ISO pitch. Give one, for example {string.Join(" or ", names)}.";
    }
}
