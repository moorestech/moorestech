using NUnit.Framework;
using MapGenerator.Pipeline.Generators.Util;
using UnityEngine;

[TestFixture]
public class PoissonDiskTests
{
    [Test]
    public void GeneratePoints_MinimumDistance_Maintained()
    {
        var points = PoissonDiskSampler.Generate(1000f, 1000f, 20f, 42);
        for (int i = 0; i < points.Count; i++)
            for (int j = i + 1; j < points.Count; j++)
            {
                float dist = Vector2.Distance(points[i], points[j]);
                Assert.That(dist, Is.GreaterThanOrEqualTo(19.9f));
            }
    }

    [Test]
    public void GeneratePoints_ProducesReasonableCount()
    {
        var points = PoissonDiskSampler.Generate(100f, 100f, 5f, 42);
        // Poisson disk packing: area / (minDist^2 * 0.7) 程度の点数になるはず
        Assert.That(points.Count, Is.GreaterThan(50));
        Assert.That(points.Count, Is.LessThan(600));
    }

    [Test]
    public void GeneratePoints_Deterministic()
    {
        var a = PoissonDiskSampler.Generate(100f, 100f, 5f, 42);
        var b = PoissonDiskSampler.Generate(100f, 100f, 5f, 42);
        Assert.AreEqual(a.Count, b.Count);
    }

    [Test]
    public void GeneratePoints_AllWithinBounds()
    {
        var points = PoissonDiskSampler.Generate(200f, 300f, 10f, 123);
        foreach (var p in points)
        {
            Assert.That(p.x, Is.InRange(0f, 200f));
            Assert.That(p.y, Is.InRange(0f, 300f));
        }
    }
}
