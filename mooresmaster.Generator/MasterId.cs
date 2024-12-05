using System;

namespace mooresmaster.Generator;

public readonly struct MasterId<T> : IEquatable<MasterId<T>>, IComparable<MasterId<T>>
{
    private static ulong _globalIndex;
    public readonly ulong Index;

    public MasterId()
    {
        Index = _globalIndex++;
    }

    public bool Equals(MasterId<T> other)
    {
        return Index == other.Index;
    }

    public override bool Equals(object? obj)
    {
        return obj is MasterId<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Index.GetHashCode();
    }

    public int CompareTo(MasterId<T> other)
    {
        return Index.CompareTo(other.Index);
    }

    public override string ToString()
    {
        return $"{{{typeof(T).Name}: {Index}}}";
    }
}
