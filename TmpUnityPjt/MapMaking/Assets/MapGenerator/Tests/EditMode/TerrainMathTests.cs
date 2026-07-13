using NUnit.Framework;
using Unity.Collections;
using MapGenerator.Pipeline.Jobs;

[TestFixture]
public class TerrainMathTests
{
    // --- FilterRange ---

    [Test]
    public void FilterRange_InsideRange_ReturnsOne()
    {
        float result = BurstTerrainMath.FilterRange(10f, 5f, 15f, 2f, 2f);
        Assert.AreEqual(1f, result, 0.001f);
    }

    [Test]
    public void FilterRange_OutsideRange_ReturnsZero()
    {
        // min=5, smoothnessMin=2 なので 5-2=3 未満で完全に0
        float result = BurstTerrainMath.FilterRange(0f, 5f, 15f, 2f, 2f);
        Assert.AreEqual(0f, result, 0.001f);
    }

    [Test]
    public void FilterRange_InLowerSmoothZone_ReturnsBetweenZeroAndOne()
    {
        // value=4 は min=5 の下側スムース帯(3〜5)の中間
        float result = BurstTerrainMath.FilterRange(4f, 5f, 15f, 2f, 2f);
        Assert.That(result, Is.InRange(0.01f, 0.99f));
    }

    [Test]
    public void FilterRange_InUpperSmoothZone_ReturnsBetweenZeroAndOne()
    {
        // value=16 は max=15 の上側スムース帯(15〜17)の中間
        float result = BurstTerrainMath.FilterRange(16f, 5f, 15f, 2f, 2f);
        Assert.That(result, Is.InRange(0.01f, 0.99f));
    }

    [Test]
    public void FilterRange_AboveUpperSmooth_ReturnsZero()
    {
        // max=15, smoothnessMax=2 なので 17 以上で完全に0
        float result = BurstTerrainMath.FilterRange(20f, 5f, 15f, 2f, 2f);
        Assert.AreEqual(0f, result, 0.001f);
    }

    // --- ComputeCurvature ---

    [Test]
    public void ComputeCurvature_FlatSurface_ReturnsZero()
    {
        // 全ピクセル同一高さ → ラプラシアン = 0
        var heights = new NativeArray<float>(25, Allocator.Temp);
        for (int i = 0; i < 25; i++) heights[i] = 0.5f;

        float curvature = BurstTerrainMath.ComputeCurvature(heights, 5, 2, 2);
        heights.Dispose();
        Assert.AreEqual(0f, curvature, 0.0001f);
    }

    [Test]
    public void ComputeCurvature_ConvexPeak_ReturnsNegative()
    {
        // 中央が高い凸形状 → ラプラシアンは負（周囲の合計 < 4×中心）
        var heights = new NativeArray<float>(25, Allocator.Temp);
        for (int i = 0; i < 25; i++) heights[i] = 0.1f;
        heights[2 * 5 + 2] = 1.0f;

        float curvature = BurstTerrainMath.ComputeCurvature(heights, 5, 2, 2);
        heights.Dispose();
        Assert.That(curvature, Is.LessThan(0f));
    }

    [Test]
    public void ComputeCurvature_ConcaveValley_ReturnsPositive()
    {
        // 中央が低い凹形状 → ラプラシアンは正（周囲の合計 > 4×中心）
        var heights = new NativeArray<float>(25, Allocator.Temp);
        for (int i = 0; i < 25; i++) heights[i] = 1.0f;
        heights[2 * 5 + 2] = 0.1f;

        float curvature = BurstTerrainMath.ComputeCurvature(heights, 5, 2, 2);
        heights.Dispose();
        Assert.That(curvature, Is.GreaterThan(0f));
    }

    [Test]
    public void ComputeCurvature_BoundaryPixel_ReturnsZero()
    {
        // 境界ピクセルは隣接データ不足で0を返す
        var heights = new NativeArray<float>(25, Allocator.Temp);
        heights[0] = 1.0f;

        Assert.AreEqual(0f, BurstTerrainMath.ComputeCurvature(heights, 5, 0, 0));
        Assert.AreEqual(0f, BurstTerrainMath.ComputeCurvature(heights, 5, 4, 4));
        heights.Dispose();
    }

    // --- ComputeSlope (NativeArray版) ---

    [Test]
    public void ComputeSlope_FlatSurface_ReturnsZero()
    {
        // 全ピクセル同一高さ → 傾斜 = 0度
        var heights = new NativeArray<float>(25, Allocator.Temp);
        for (int i = 0; i < 25; i++) heights[i] = 0.5f;

        float slope = BurstTerrainMath.ComputeSlope(heights, 5, 2, 2, 100f, 100f, 100f);
        heights.Dispose();
        Assert.AreEqual(0f, slope, 0.001f);
    }

    [Test]
    public void ComputeSlope_SteepSlope_ReturnsHighAngle()
    {
        // X方向に大きな高さ差 → 急斜面
        var heights = new NativeArray<float>(9, Allocator.Temp);
        for (int i = 0; i < 9; i++) heights[i] = 0f;
        heights[1 * 3 + 1] = 0.0f;
        heights[1 * 3 + 2] = 1.0f;

        float slope = BurstTerrainMath.ComputeSlope(heights, 3, 1, 1, 100f, 500f, 100f);
        heights.Dispose();
        Assert.That(slope, Is.GreaterThan(45f));
    }

    // --- BurstTerrainMath ---

    [Test]
    public void BurstSmoothstep_MidpointIsHalf()
    {
        float v = BurstTerrainMath.Smoothstep(0f, 1f, 0.5f);
        Assert.AreEqual(0.5f, v, 0.01f);
    }

    [Test]
    public void BurstFilterRange_InsideRange_ReturnsOne()
    {
        float v = BurstTerrainMath.FilterRange(45f, 30f, 60f, 5f, 5f);
        Assert.AreEqual(1f, v, 0.01f);
    }

    [Test]
    public void BurstComputeSlope_FlatTerrain_ReturnsZero()
    {
        var heights = new NativeArray<float>(9, Allocator.Temp);
        for (int i = 0; i < 9; i++) heights[i] = 0.5f;
        float slope = BurstTerrainMath.ComputeSlope(heights, 3, 1, 1, 100f, 100f, 100f);
        heights.Dispose();
        Assert.AreEqual(0f, slope, 0.01f);
    }

    [Test]
    public void BurstComputeCurvature_FlatTerrain_ReturnsZero()
    {
        var heights = new NativeArray<float>(9, Allocator.Temp);
        for (int i = 0; i < 9; i++) heights[i] = 0.5f;
        float c = BurstTerrainMath.ComputeCurvature(heights, 3, 1, 1);
        heights.Dispose();
        Assert.AreEqual(0f, c, 0.001f);
    }
}
