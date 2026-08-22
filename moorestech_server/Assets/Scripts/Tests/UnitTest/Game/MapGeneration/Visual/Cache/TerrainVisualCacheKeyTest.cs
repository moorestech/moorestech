using System;
using Game.MapGeneration.Cache;
using Game.MapGeneration.Transfer;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Cache
{
    /// <summary>
    ///     キーが入力5項目全てに反応することを検証
    ///     1つでも取りこぼすと、入力が動いたのにキャッシュがヒットして古いsplatmap/detailが描かれる
    ///     Verifies the key reacts to all five generation inputs (fingerprint, seed, two origins, resolution, generator version);
    ///     missing even one would let the cache hit after an input moved, drawing a stale splatmap and detail
    /// </summary>
    public class TerrainVisualCacheKeyTest
    {
        private const string Fingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private const int Seed = 12345;
        private const int Resolution = 513;
        private const string GeneratorVersion = "2.0.0";

        private static readonly TerrainOrigins Origins = new(new Vector2(1024f, -2048f), new Vector2(64f, -128f));

        [Test]
        public void ProducesTheSameKeyForTheSameInputs()
        {
            Assert.That(Compute(Fingerprint, Seed, Origins, Resolution, GeneratorVersion),
                Is.EqualTo(Compute(Fingerprint, Seed, Origins, Resolution, GeneratorVersion)));
        }

        [Test]
        public void ProducesA64CharacterLowercaseHexKey()
        {
            // 書き手はこの長さ固定を前提にヘッダ領域を確保する。長さが動くと形式ごと壊れる
            // The writer reserves its header field assuming this fixed length, so a moved length breaks the format itself
            var key = Compute(Fingerprint, Seed, Origins, Resolution, GeneratorVersion);

            Assert.That(key.Length, Is.EqualTo(64));
            Assert.That(key, Does.Match("^[0-9a-f]{64}$"));
        }

        [Test]
        public void ChangesWhenTheFingerprintChanges()
        {
            var otherFingerprint = Fingerprint.Replace('0', '1');

            Assert.That(Compute(otherFingerprint, Seed, Origins, Resolution, GeneratorVersion),
                Is.Not.EqualTo(Compute(Fingerprint, Seed, Origins, Resolution, GeneratorVersion)));
        }

        [Test]
        public void ChangesWhenTheSeedChanges()
        {
            Assert.That(Compute(Fingerprint, Seed + 1, Origins, Resolution, GeneratorVersion),
                Is.Not.EqualTo(Compute(Fingerprint, Seed, Origins, Resolution, GeneratorVersion)));
        }

        [Test]
        public void ChangesWhenTheNoiseOriginMovesOnEitherAxis()
        {
            // splatmapはノイズ窓原点を直接引数に取る。原点が動けば同じ高さでも別の分類になる
            // The splatmap takes the noise window origin as a direct argument, so a moved origin reclassifies the same heights
            var baseKey = Compute(Fingerprint, Seed, Origins, Resolution, GeneratorVersion);
            var movedX = new TerrainOrigins(new Vector2(Origins.NoiseOrigin.x + 1f, Origins.NoiseOrigin.y), Origins.SceneOrigin);
            var movedZ = new TerrainOrigins(new Vector2(Origins.NoiseOrigin.x, Origins.NoiseOrigin.y + 1f), Origins.SceneOrigin);

            Assert.That(Compute(Fingerprint, Seed, movedX, Resolution, GeneratorVersion), Is.Not.EqualTo(baseKey), "X方向のずれを取りこぼした");
            Assert.That(Compute(Fingerprint, Seed, movedZ, Resolution, GeneratorVersion), Is.Not.EqualTo(baseKey), "Z方向のずれを取りこぼした");
        }

        [Test]
        public void ChangesWhenTheSceneOriginMovesOnEitherAxis()
        {
            var baseKey = Compute(Fingerprint, Seed, Origins, Resolution, GeneratorVersion);
            var movedX = new TerrainOrigins(Origins.NoiseOrigin, new Vector2(Origins.SceneOrigin.x + 1f, Origins.SceneOrigin.y));
            var movedZ = new TerrainOrigins(Origins.NoiseOrigin, new Vector2(Origins.SceneOrigin.x, Origins.SceneOrigin.y + 1f));

            Assert.That(Compute(Fingerprint, Seed, movedX, Resolution, GeneratorVersion), Is.Not.EqualTo(baseKey), "X方向のずれを取りこぼした");
            Assert.That(Compute(Fingerprint, Seed, movedZ, Resolution, GeneratorVersion), Is.Not.EqualTo(baseKey), "Z方向のずれを取りこぼした");
        }

        [Test]
        public void ChangesWhenTheResolutionChanges()
        {
            Assert.That(Compute(Fingerprint, Seed, Origins, Resolution + 1, GeneratorVersion),
                Is.Not.EqualTo(Compute(Fingerprint, Seed, Origins, Resolution, GeneratorVersion)));
        }

        [Test]
        public void ChangesWhenTheGeneratorVersionChanges()
        {
            Assert.That(Compute(Fingerprint, Seed, Origins, Resolution, "9.9.9"),
                Is.Not.EqualTo(Compute(Fingerprint, Seed, Origins, Resolution, GeneratorVersion)));
        }

        [Test]
        public void DistinguishesAxesThatAreMerelySwapped()
        {
            // 2軸を1つの値へ潰していると、入れ替えただけの別の窓が同じキーになる
            // Collapsing the two axes into one value would give a merely swapped window the same key
            var swapped = new TerrainOrigins(new Vector2(Origins.NoiseOrigin.y, Origins.NoiseOrigin.x), Origins.SceneOrigin);

            Assert.That(Compute(Fingerprint, Seed, swapped, Resolution, GeneratorVersion),
                Is.Not.EqualTo(Compute(Fingerprint, Seed, Origins, Resolution, GeneratorVersion)));
        }

        [Test]
        public void ChangesWhenTheNoiseOriginMovesByAFraction()
        {
            // 桁を落とした表記だと約2km離れた窓と1cmずれた窓が区別できなくなる。往復可能な表記を担保する
            // A truncated notation could not tell a 1cm shift from a 2km one; this pins the round-trippable form
            var fractional = new TerrainOrigins(new Vector2(1024.0001f, -2048f), Origins.SceneOrigin);

            Assert.That(Compute(Fingerprint, Seed, fractional, Resolution, GeneratorVersion),
                Is.Not.EqualTo(Compute(Fingerprint, Seed, Origins, Resolution, GeneratorVersion)));
        }

        [Test]
        public void ThrowsWhenTheFingerprintIsMissing()
        {
            // 空の指紋で黙ってキーを作ると、マスタ未ロードの取り違えが「なぜか毎回ヒットする」形で現れる
            // Silently keying an empty fingerprint would surface an unloaded-master mixup as an inexplicable permanent hit
            Assert.Throws<InvalidOperationException>(() => Compute(null, Seed, Origins, Resolution, GeneratorVersion));
            Assert.Throws<InvalidOperationException>(() => Compute(string.Empty, Seed, Origins, Resolution, GeneratorVersion));
        }

        [Test]
        public void ThrowsWhenTheGeneratorVersionIsMissing()
        {
            Assert.Throws<InvalidOperationException>(() => Compute(Fingerprint, Seed, Origins, Resolution, null));
            Assert.Throws<InvalidOperationException>(() => Compute(Fingerprint, Seed, Origins, Resolution, string.Empty));
        }

        private static string Compute(string generationMasterFingerprint, int seed, TerrainOrigins origins, int terrainResolution, string generatorVersion)
        {
            return TerrainVisualCacheKey.Compute(generationMasterFingerprint, seed, origins, terrainResolution, generatorVersion);
        }
    }
}
