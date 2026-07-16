using NUnit.Framework;
using MapGenerator.Pipeline.Jobs;

/// <summary>
/// BurstTerrainMath.FilterRangeを使ったテクスチャフィルタロジックの検証。
/// 旧SplatmapFilter.ComputeTextureWeightsの責務はSplatmapJobに移行済みだが、
/// フィルタの数学的正しさを単体テストで担保する。
/// </summary>
[TestFixture]
public class SplatmapFilterTests
{
    // --- 斜面フィルタ ---

    [Test]
    public void SlopeFilter_InsideRange_ReturnsOne()
    {
        // slope=25はslopeMin=10, slopeMax=40の範囲内 → フィルタ値は1.0
        float result = BurstTerrainMath.FilterRange(25f, 10f, 40f, 2f, 2f);
        Assert.AreEqual(1f, result, 0.01f);
    }

    [Test]
    public void SlopeFilter_OutsideRange_ReturnsZero()
    {
        // slope=0はslopeMin=30の遥か下 → フィルタ値はほぼ0
        float result = BurstTerrainMath.FilterRange(0f, 30f, 60f, 2f, 2f);
        Assert.AreEqual(0f, result, 0.001f);
    }

    [Test]
    public void SlopeFilter_InSmoothZone_ReturnsBetweenZeroAndOne()
    {
        // slope=29はslopeMin=30のスムース帯(28〜30)の中 → 0〜1の間
        float result = BurstTerrainMath.FilterRange(29f, 30f, 60f, 2f, 2f);
        Assert.That(result, Is.InRange(0.01f, 0.99f));
    }

    // --- 高度フィルタ ---

    [Test]
    public void HeightFilter_InsideRange_ReturnsOne()
    {
        // height=0.5はheightMin=0.2, heightMax=0.8の範囲内 → フィルタ値は1.0
        float result = BurstTerrainMath.FilterRange(0.5f, 0.2f, 0.8f, 0.02f, 0.02f);
        Assert.AreEqual(1f, result, 0.01f);
    }

    [Test]
    public void HeightFilter_OutsideRange_ReturnsZero()
    {
        // height=0.1はheightMin=0.3の遥か下 → フィルタ値はほぼ0
        float result = BurstTerrainMath.FilterRange(0.1f, 0.3f, 0.8f, 0.02f, 0.02f);
        Assert.AreEqual(0f, result, 0.01f);
    }

    [Test]
    public void HeightFilter_AboveRange_ReturnsZero()
    {
        // height=0.95はheightMax=0.8+smoothness=0.02の遥か上 → フィルタ値はほぼ0
        float result = BurstTerrainMath.FilterRange(0.95f, 0.2f, 0.8f, 0.02f, 0.02f);
        Assert.AreEqual(0f, result, 0.01f);
    }

    // --- 曲率フィルタ ---

    [Test]
    public void CurvatureFilter_InsideRange_ReturnsOne()
    {
        // curvature=0.1はcurvatureMin=-0.5, curvatureMax=0.5の範囲内 → 1.0
        float result = BurstTerrainMath.FilterRange(0.1f, -0.5f, 0.5f, 0.1f, 0.1f);
        Assert.AreEqual(1f, result, 0.01f);
    }

    [Test]
    public void CurvatureFilter_OutsideRange_ReturnsZero()
    {
        // curvature=0.8はcurvatureMax=0.2の遥か上 → フィルタ値はほぼ0
        float result = BurstTerrainMath.FilterRange(0.8f, -0.2f, 0.2f, 0.1f, 0.1f);
        Assert.AreEqual(0f, result, 0.01f);
    }

    // --- 複合フィルタ（乗算検証） ---

    [Test]
    public void CombinedFilters_MultiplyReducesWeight()
    {
        // 斜面フィルタ×高度フィルタの乗算が単独より小さいことを検証
        float slopeFilter = BurstTerrainMath.FilterRange(29f, 30f, 60f, 2f, 2f);
        float heightFilter = BurstTerrainMath.FilterRange(0.5f, 0.2f, 0.8f, 0.02f, 0.02f);
        float combined = slopeFilter * heightFilter;

        // slopeFilterは遷移帯なので<1、heightFilterは範囲内で=1
        Assert.That(combined, Is.LessThanOrEqualTo(slopeFilter + 0.001f));
        Assert.That(combined, Is.LessThanOrEqualTo(heightFilter + 0.001f));
    }

    [Test]
    public void Smoothstep_Symmetry_LowerAndUpperMatch()
    {
        // 対称性テスト: 下端と上端のスムース帯が同じ形状になることを検証
        float lower = BurstTerrainMath.FilterRange(4f, 5f, 15f, 2f, 2f);
        float upper = BurstTerrainMath.FilterRange(16f, 5f, 15f, 2f, 2f);
        // 対称なsmoothness幅なので、等距離の点では同じフィルタ値になるはず
        Assert.AreEqual(lower, upper, 0.01f);
    }
}
