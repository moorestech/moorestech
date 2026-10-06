using Game.MapGeneration.Cache;
using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Transfer;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Surface
{
    public class SurfaceDisplayCacheRevisionTest
    {
        [Test]
        public void OwnedDisplayInvalidatesEarlierGroundedCacheButPreservesLegacyKey()
        {
            var legacy = MapGenerationAlgorithmTable.ResolveSurface(WorldSurfaceRevision.Legacy4);
            var grounded = MapGenerationAlgorithmTable.ResolveSurface(WorldSurfaceRevision.Grounded5);
            Assert.That(legacy.VisualCacheVersionSuffix, Is.Empty);
            Assert.That(Key("4.0.0" + legacy.VisualCacheVersionSuffix), Is.EqualTo(Key("4.0.0")));
            Assert.That(Key("5.0.0" + grounded.VisualCacheVersionSuffix), Is.Not.EqualTo(Key("5.0.0")));
        }

        private static string Key(string version)
        {
            return TerrainVisualCacheKey.Compute(new string('a', 64), 196,
                new TerrainOrigins(Vector2.zero, Vector2.zero), 33, version, new string('b', 64));
        }
    }
}
