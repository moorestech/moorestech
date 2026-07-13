using NUnit.Framework;
using MapGenerator.Pipeline.Config;
using MapGenerator.Pipeline.Generators;

[TestFixture]
public class OreBandPlannerTests
{
    static OreBand Band(float outer) => new OreBand { outerRadiusMeters = outer };

    [Test]
    public void SingleInfiniteBand_CoversWholeRange()
    {
        var r = OreBandPlanner.BuildRanges(new[] { Band(-1f) });
        Assert.AreEqual(1, r.Count);
        Assert.AreEqual(0f, r[0].Inner);
        Assert.IsTrue(float.IsPositiveInfinity(r[0].Outer));
    }

    [Test]
    public void TwoBands_ProduceContiguousRings()
    {
        var r = OreBandPlanner.BuildRanges(new[] { Band(200f), Band(-1f) });
        Assert.AreEqual(2, r.Count);
        Assert.AreEqual(0f, r[0].Inner);
        Assert.AreEqual(200f, r[0].Outer);
        Assert.AreEqual(200f, r[1].Inner);
        Assert.IsTrue(float.IsPositiveInfinity(r[1].Outer));
    }

    [Test]
    public void UnsortedInput_IsSortedAscending()
    {
        var r = OreBandPlanner.BuildRanges(new[] { Band(-1f), Band(200f), Band(50f) });
        Assert.AreEqual(3, r.Count);
        Assert.AreEqual(0f, r[0].Inner); Assert.AreEqual(50f, r[0].Outer);
        Assert.AreEqual(50f, r[1].Inner); Assert.AreEqual(200f, r[1].Outer);
        Assert.AreEqual(200f, r[2].Inner); Assert.IsTrue(float.IsPositiveInfinity(r[2].Outer));
    }

    [Test]
    public void DuplicateOuter_SecondIsSkipped()
    {
        var r = OreBandPlanner.BuildRanges(new[] { Band(100f), Band(100f) });
        Assert.AreEqual(1, r.Count);
        Assert.AreEqual(0f, r[0].Inner);
        Assert.AreEqual(100f, r[0].Outer);
    }

    [Test]
    public void ZeroOuter_DropsRange()
    {
        // outerRadiusMeters = 0 は Outer(0) > Inner(0) を満たさず縮退で除外される
        Assert.AreEqual(0, OreBandPlanner.BuildRanges(new[] { Band(0f) }).Count);
    }

    [Test]
    public void EmptyOrNull_ReturnsEmpty()
    {
        Assert.AreEqual(0, OreBandPlanner.BuildRanges(null).Count);
        Assert.AreEqual(0, OreBandPlanner.BuildRanges(new OreBand[0]).Count);
    }

    [Test]
    public void NullElement_IsSkipped()
    {
        var r = OreBandPlanner.BuildRanges(new[] { null, Band(100f), null });
        Assert.AreEqual(1, r.Count);
        Assert.AreEqual(100f, r[0].Outer);
    }

    [Test]
    public void NaNOuter_IsSkipped_DoesNotPoisonOtherBands()
    {
        // NaN の outerRadiusMeters を残すと inner=NaN で後続バンドが全消えする回帰を防ぐ
        var r = OreBandPlanner.BuildRanges(new[] { Band(float.NaN), Band(100f), Band(-1f) });
        Assert.AreEqual(2, r.Count);
        Assert.AreEqual(0f, r[0].Inner); Assert.AreEqual(100f, r[0].Outer);
        Assert.AreEqual(100f, r[1].Inner); Assert.IsTrue(float.IsPositiveInfinity(r[1].Outer));
    }

    [Test]
    public void MultipleInfinite_OnlyFirstKept()
    {
        var r = OreBandPlanner.BuildRanges(new[] { Band(-1f), Band(-1f), Band(100f) });
        Assert.AreEqual(2, r.Count);
        Assert.IsTrue(float.IsPositiveInfinity(r[1].Outer));
    }

    [Test]
    public void StableSort_FirstDuplicateByArrayOrderWins()
    {
        var first = Band(100f); var second = Band(100f);
        var r = OreBandPlanner.BuildRanges(new[] { first, second });
        Assert.AreEqual(1, r.Count);
        Assert.AreSame(first, r[0].Band);
    }

    [Test]
    public void Contains_InnerInclusive_OuterExclusive()
    {
        var range = new OreBandRange(Band(200f), 50f, 200f);
        Assert.IsFalse(range.Contains(49.9f));
        Assert.IsTrue(range.Contains(50f));
        Assert.IsTrue(range.Contains(199.9f));
        Assert.IsFalse(range.Contains(200f));
    }

    [Test]
    public void Contains_InfiniteOuter_AlwaysTrueAboveInner()
    {
        var range = new OreBandRange(Band(-1f), 200f, float.PositiveInfinity);
        Assert.IsFalse(range.Contains(199f));
        Assert.IsTrue(range.Contains(200f));
        Assert.IsTrue(range.Contains(1e9f));
    }
}
