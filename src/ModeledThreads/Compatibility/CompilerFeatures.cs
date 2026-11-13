// The .NET Framework 4.8 does not carry these, but the compiler needs them to
// write init-only setters, required members and range expressions.

namespace System.Runtime.CompilerServices
{
    /// <summary>Marks an init-only setter.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }

    /// <summary>Marks a member that has to be set when the object is made.</summary>
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property,
        AllowMultiple = false,
        Inherited = false)]
    internal sealed class RequiredMemberAttribute : Attribute
    {
    }

    /// <summary>Names a compiler feature a member needs.</summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName)
        {
            FeatureName = featureName;
        }

        public string FeatureName { get; }

        public bool IsOptional { get; set; }
    }
}

namespace System
{
    /// <summary>A place in a sequence, counted from either end.</summary>
    public readonly struct Index : IEquatable<Index>
    {
        private readonly int _value;

        public Index(int value, bool fromEnd = false)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _value = fromEnd ? ~value : value;
        }

        private Index(int value)
        {
            _value = value;
        }

        public static Index Start => new(0);

        public static Index End => new(~0);

        public static Index FromStart(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            return new Index(value);
        }

        public static Index FromEnd(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            return new Index(~value);
        }

        public int Value => _value < 0 ? ~_value : _value;

        public bool IsFromEnd => _value < 0;

        public int GetOffset(int length) => _value < 0 ? length + _value + 1 : _value;

        public static implicit operator Index(int value) => FromStart(value);

        public bool Equals(Index other) => _value == other._value;

        public override bool Equals(object? value) => value is Index index && _value == index._value;

        public override int GetHashCode() => _value;

        public override string ToString() => IsFromEnd ? "^" + (uint)Value : Value.ToString();
    }

    /// <summary>A span of a sequence.</summary>
    public readonly struct Range : IEquatable<Range>
    {
        public Range(Index start, Index end)
        {
            Start = start;
            End = end;
        }

        public Index Start { get; }

        public Index End { get; }

        public static Range StartAt(Index start) => new(start, Index.End);

        public static Range EndAt(Index end) => new(Index.Start, end);

        public static Range All => new(Index.Start, Index.End);

        public (int Offset, int Length) GetOffsetAndLength(int length)
        {
            int start = Start.GetOffset(length);
            int end = End.GetOffset(length);

            if ((uint)end > (uint)length || (uint)start > (uint)end)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            return (start, end - start);
        }

        public bool Equals(Range other) => Start.Equals(other.Start) && End.Equals(other.End);

        public override bool Equals(object? value) => value is Range range && Equals(range);

        public override int GetHashCode() => (Start.GetHashCode() * 31) + End.GetHashCode();

        public override string ToString() => Start + ".." + End;
    }
}
