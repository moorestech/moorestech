using System;

namespace mooresmaster.Generator;

public readonly struct MasterId : IEquatable<MasterId>, IComparable<MasterId>
{
    private static ulong _globalIndex;
    public readonly ulong Index;

    public MasterId()
    {
        Index = _globalIndex++;
    }

    public bool Equals(MasterId other)
    {
        return Index == other.Index;
    }

    public override bool Equals(object? obj)
    {
        return obj is MasterId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Index.GetHashCode();
    }

    public int CompareTo(MasterId other)
    {
        return Index.CompareTo(other.Index);
    }

    public override string ToString()
    {
        return Index.ToString();
    }
}
