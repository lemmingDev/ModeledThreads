using System.Globalization;
using System.Text;
using Inventor;

namespace ModeledThreads.Core;

/// <summary>
/// The inputs a thread was made from, stored on the thread's own features.
///
/// It lives in the document as a plain attribute set, so a part that is saved
/// and opened elsewhere still carries it, and a part opened without the add-in
/// is unaffected: nothing but text is added, and nothing depends on it.
/// </summary>
public sealed class ThreadRecord
{
    /// <summary>Identifies the features that belong to one thread.</summary>
    public required string ThreadId { get; init; }

    /// <summary>The size as it was typed, e.g. "M10x1.25".</summary>
    public required string Designation { get; init; }

    /// <summary>The threaded length, in millimetres; 0 for the whole face.</summary>
    public double LengthMm { get; init; }

    /// <summary>The clearance, in millimetres.</summary>
    public double ClearanceMm { get; init; }

    public int Starts { get; init; } = 1;

    public bool RightHanded { get; init; } = true;

    public bool ChamferStart { get; init; } = true;

    public bool ChamferEnd { get; init; } = true;

    /// <summary>True when the thread starts at the face's high end.</summary>
    public bool ReverseStart { get; init; }

    public bool Internal { get; init; }

    /// <summary>The axis direction of the face the thread is on.</summary>
    public double AxisX { get; init; }

    public double AxisY { get; init; }

    public double AxisZ { get; init; }

    /// <summary>A point on that axis, in centimetres.</summary>
    public double BaseX { get; init; }

    public double BaseY { get; init; }

    public double BaseZ { get; init; }

    /// <summary>The face's radius, in centimetres.</summary>
    public double RadiusCm { get; init; }

    /// <summary>The face's extent along the axis, in centimetres.</summary>
    public double LowCm { get; init; }

    public double HighCm { get; init; }

    /// <summary>What the face met at its low end.</summary>
    public ThreadEndKind LowEnd { get; init; }

    /// <summary>What the face met at its high end.</summary>
    public ThreadEndKind HighEnd { get; init; }

    /// <summary>True when the face's normal pointed out of the material.</summary>
    public bool NormalIsOutward { get; init; } = true;
}

/// <summary>
/// Reads and writes <see cref="ThreadRecord"/>s through the features' attribute
/// sets, and finds the features that make up a thread.
///
/// The features are handled as plain objects: the interop gives each feature
/// type its own interface with no shared base, so the members that every
/// feature does have — its attributes, its name, its delete — are reached late
/// bound.
/// </summary>
public static class ThreadMetadata
{
    /// <summary>The name of the attribute set every modelled thread carries.</summary>
    public const string SetName = "ModeledThreads";

    private const string AttributeName = "Thread";

    /// <summary>Writes a record onto a feature, replacing any it already has.</summary>
    public static void Write(object feature, ThreadRecord record)
    {
        AttributeSets sets = ((dynamic)feature).AttributeSets;

        AttributeSet set = sets.NameIsUsed[SetName] ? sets[SetName] : sets.Add(SetName);

        string text = Serialize(record);
        if (set.NameIsUsed[AttributeName])
        {
            set[AttributeName].Value = text;
        }
        else
        {
            set.Add(AttributeName, ValueTypeEnum.kStringType, text);
        }
    }

    /// <summary>Reads the record a feature carries, if it carries one.</summary>
    public static bool TryRead(object? feature, out ThreadRecord? record)
    {
        record = null;

        try
        {
            if (feature is null)
            {
                return false;
            }

            AttributeSets sets = ((dynamic)feature).AttributeSets;
            if (!sets.NameIsUsed[SetName])
            {
                return false;
            }

            AttributeSet set = sets[SetName];
            if (!set.NameIsUsed[AttributeName])
            {
                return false;
            }

            string? text = set[AttributeName].Value as string;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            record = Deserialize(text!);
            return record is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Reads the record from something the user selected: one of the thread's
    /// features, or a face that one of them made.
    /// </summary>
    public static bool TryReadFrom(object? selected, out ThreadRecord? record, out object? feature)
    {
        record = null;
        feature = null;

        // A click in the graphics window can land on a face or on an edge; an
        // edge is part of the thread if either of the faces beside it is.
        if (selected is Edge edge)
        {
            try
            {
                foreach (Face beside in edge.Faces)
                {
                    if (TryRead(CreatedBy(beside), out record))
                    {
                        feature = CreatedBy(beside);
                        return true;
                    }
                }
            }
            catch
            {
                // an edge with unreadable faces belongs to no thread
            }

            return false;
        }

        object? candidate = selected is Face face ? CreatedBy(face) : selected;

        if (!TryRead(candidate, out record))
        {
            return false;
        }

        feature = candidate;
        return true;
    }

    /// <summary>The feature that made a face, if one did.</summary>
    private static object? CreatedBy(Face face)
    {
        try
        {
            return face.CreatedByFeature;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Every feature of a thread: the coil and its end cuts.</summary>
    public static List<object> Features(PartComponentDefinition definition, string threadId)
    {
        var found = new List<object>();

        PartFeatures features = definition.Features;
        for (int i = 1; i <= features.Count; i++)
        {
            object feature = features[i];
            if (TryRead(feature, out ThreadRecord? record) && record?.ThreadId == threadId)
            {
                found.Add(feature);
            }
        }

        return found;
    }

    /// <summary>The name of a feature, or a placeholder if it cannot be read.</summary>
    public static string NameOf(object? feature)
    {
        try
        {
            return feature is null ? "(unknown)" : (string)((dynamic)feature).Name;
        }
        catch
        {
            return "(unknown)";
        }
    }

    /// <summary>Deletes a feature, keeping anything that depends on it.</summary>
    public static void Delete(object feature)
    {
        ((dynamic)feature).Delete(false, true, true);
    }

    private static string Serialize(ThreadRecord record)
    {
        var text = new StringBuilder();
        void Add(string key, string value) => text.Append(key).Append('=').Append(value).Append('|');
        void Number(string key, double value) => Add(key, value.ToString("0.#######", CultureInfo.InvariantCulture));
        void Flag(string key, bool value) => Add(key, value ? "1" : "0");

        Add("id", record.ThreadId);
        Add("designation", record.Designation);
        Number("length", record.LengthMm);
        Number("clearance", record.ClearanceMm);
        Add("starts", record.Starts.ToString(CultureInfo.InvariantCulture));
        Flag("right", record.RightHanded);
        Flag("chamferStart", record.ChamferStart);
        Flag("chamferEnd", record.ChamferEnd);
        Flag("reverse", record.ReverseStart);
        Flag("internal", record.Internal);
        Number("axisX", record.AxisX);
        Number("axisY", record.AxisY);
        Number("axisZ", record.AxisZ);
        Number("baseX", record.BaseX);
        Number("baseY", record.BaseY);
        Number("baseZ", record.BaseZ);
        Number("radius", record.RadiusCm);
        Number("low", record.LowCm);
        Number("high", record.HighCm);
        Add("lowEnd", ((int)record.LowEnd).ToString(CultureInfo.InvariantCulture));
        Add("highEnd", ((int)record.HighEnd).ToString(CultureInfo.InvariantCulture));
        Flag("outward", record.NormalIsOutward);

        return text.ToString();
    }

    private static ThreadRecord? Deserialize(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (string pair in text.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = pair.IndexOf('=');
            if (equals > 0)
            {
                values[pair[..equals]] = pair[(equals + 1)..];
            }
        }

        if (!values.TryGetValue("id", out string? id) || !values.TryGetValue("designation", out string? designation))
        {
            return null;
        }

        double Number(string key) =>
            values.TryGetValue(key, out string? value)
            && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                ? parsed
                : 0.0;

        int Integer(string key) =>
            values.TryGetValue(key, out string? value)
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : 1;

        bool Flag(string key) => values.TryGetValue(key, out string? value) && value == "1";

        ThreadEndKind End(string key) =>
            values.TryGetValue(key, out string? value)
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            && Enum.IsDefined(typeof(ThreadEndKind), parsed)
                ? (ThreadEndKind)parsed
                : ThreadEndKind.Free;

        return new ThreadRecord
        {
            ThreadId = id,
            Designation = designation,
            LengthMm = Number("length"),
            ClearanceMm = Number("clearance"),
            Starts = Integer("starts"),
            RightHanded = Flag("right"),
            ChamferStart = Flag("chamferStart"),
            ChamferEnd = Flag("chamferEnd"),
            ReverseStart = Flag("reverse"),
            Internal = Flag("internal"),
            AxisX = Number("axisX"),
            AxisY = Number("axisY"),
            AxisZ = Number("axisZ"),
            BaseX = Number("baseX"),
            BaseY = Number("baseY"),
            BaseZ = Number("baseZ"),
            RadiusCm = Number("radius"),
            LowCm = Number("low"),
            HighCm = Number("high"),
            LowEnd = End("lowEnd"),
            HighEnd = End("highEnd"),
            NormalIsOutward = Flag("outward")
        };
    }
}
