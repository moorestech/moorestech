using System;
using Client.Game.InGame.Environment.Terrain.Visual.Cache;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UnitTest.Terrain
{
    /// <summary>
    ///     キーが導出元5つすべてに反応することを検証する。1つでも取りこぼすと、導出元が動いたのに
    ///     キャッシュがヒットして古いsplatmap/detailが描かれる
    ///     Verifies the key reacts to all five inputs; missing even one would let the cache hit after an input moved,
    ///     drawing a stale splatmap and detail
    /// </summary>
    public class TerrainVisualCacheKeyTest
    {
        private const string MasterJsonText = "{\"generations\":[{\"algorithm\":\"Islands\"}]}";
        private const string TerrainHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private const int Seed = 12345;

        private static readonly Vector2 NoiseOrigin = new(1024f, -2048f);

        // MapObjectsDigestが返すのと同じ32バイト。ここでは中身ではなく「キーが反応するか」だけを見る
        // The same 32 bytes MapObjectsDigest returns; what matters here is whether the key reacts, not the content
        private static readonly byte[] MapObjectsDigest = new byte[32];

        [Test]
        public void ProducesTheSameKeyForTheSameInputs()
        {
            Assert.That(Compute(MasterJsonText, TerrainHash, NoiseOrigin, Seed),
                Is.EqualTo(Compute(MasterJsonText, TerrainHash, NoiseOrigin, Seed)));
        }

        [Test]
        public void ProducesA64CharacterLowercaseHexKey()
        {
            // 書き手はこの長さ固定を前提にヘッダ領域を確保する。長さが動くと形式ごと壊れる
            // The writer reserves its header field assuming this fixed length, so a moved length breaks the format itself
            var key = Compute(MasterJsonText, TerrainHash, NoiseOrigin, Seed);

            Assert.That(key.Length, Is.EqualTo(64));
            Assert.That(key, Does.Match("^[0-9a-f]{64}$"));
        }

        [Test]
        public void ChangesWhenTheGenerationMasterChanges()
        {
            // マスタはdetailプロトタイプ順もsplatmapのレイヤー順も決める。原文が動けば添字の意味が変わる
            // The master fixes both the detail prototype order and the splatmap layer order, so a moved text remaps every index
            Assert.That(Compute(MasterJsonText + " ", TerrainHash, NoiseOrigin, Seed),
                Is.Not.EqualTo(Compute(MasterJsonText, TerrainHash, NoiseOrigin, Seed)));
        }

        [Test]
        public void ChangesWhenTheTerrainHashChanges()
        {
            var otherHash = TerrainHash.Replace('0', '1');

            Assert.That(Compute(MasterJsonText, otherHash, NoiseOrigin, Seed),
                Is.Not.EqualTo(Compute(MasterJsonText, TerrainHash, NoiseOrigin, Seed)));
        }

        [Test]
        public void ChangesWhenTheSeedChanges()
        {
            Assert.That(Compute(MasterJsonText, TerrainHash, NoiseOrigin, Seed + 1),
                Is.Not.EqualTo(Compute(MasterJsonText, TerrainHash, NoiseOrigin, Seed)));
        }

        [Test]
        public void ChangesWhenTheNoiseOriginMovesOnEitherAxis()
        {
            // splatmapはノイズ窓原点を直接引数に取る。原点が動けば同じ高さでも別の分類になる
            // The splatmap takes the noise window origin as a direct argument, so a moved origin reclassifies the same heights
            var baseKey = Compute(MasterJsonText, TerrainHash, NoiseOrigin, Seed);

            Assert.That(Compute(MasterJsonText, TerrainHash, new Vector2(NoiseOrigin.x + 1f, NoiseOrigin.y), Seed),
                Is.Not.EqualTo(baseKey), "X方向のずれを取りこぼした");
            Assert.That(Compute(MasterJsonText, TerrainHash, new Vector2(NoiseOrigin.x, NoiseOrigin.y + 1f), Seed),
                Is.Not.EqualTo(baseKey), "Z方向のずれを取りこぼした");
        }

        [Test]
        public void DistinguishesAxesThatAreMerelySwapped()
        {
            // 2軸を1つの値へ潰していると、入れ替えただけの別の窓が同じキーになる
            // Collapsing the two axes into one value would give a merely swapped window the same key
            Assert.That(Compute(MasterJsonText, TerrainHash, new Vector2(NoiseOrigin.y, NoiseOrigin.x), Seed),
                Is.Not.EqualTo(Compute(MasterJsonText, TerrainHash, NoiseOrigin, Seed)));
        }

        [Test]
        public void SeparatesTheMasterTextFromTheTerrainHash()
        {
            // 可変長のマスタ原文を生のまま連結すると、区切りを跨いだ別の組み合わせが同じキーに落ちる
            // Joining the variable-length master text raw would collapse a differently split pair onto the same key
            Assert.That(Compute(MasterJsonText + "|" + TerrainHash, TerrainHash, NoiseOrigin, Seed),
                Is.Not.EqualTo(Compute(MasterJsonText, TerrainHash + "|" + TerrainHash, NoiseOrigin, Seed)));
        }

        [Test]
        public void ChangesWhenTheNoiseOriginMovesByAFraction()
        {
            // 桁を落とした表記だと約2km離れた窓と1cmずれた窓が区別できなくなる。往復可能な表記を担保する
            // A truncated notation could not tell a 1cm shift from a 2km one; this pins the round-trippable form
            Assert.That(Compute(MasterJsonText, TerrainHash, new Vector2(1024.0001f, -2048f), Seed),
                Is.Not.EqualTo(Compute(MasterJsonText, TerrainHash, NoiseOrigin, Seed)));
        }

        [Test]
        public void ThrowsWhenTheGenerationMasterTextIsMissing()
        {
            // 空のマスタで黙ってキーを作ると、マスタ未ロードの取り違えが「なぜか毎回ヒットする」形で現れる
            // Silently keying an empty master would surface an unloaded-master mixup as an inexplicable permanent hit
            Assert.Throws<InvalidOperationException>(() => Compute(null, TerrainHash, NoiseOrigin, Seed));
            Assert.Throws<InvalidOperationException>(() => Compute(string.Empty, TerrainHash, NoiseOrigin, Seed));
        }

        [Test]
        public void ThrowsWhenTheTerrainHashIsMissing()
        {
            Assert.Throws<InvalidOperationException>(() => Compute(MasterJsonText, null, NoiseOrigin, Seed));
            Assert.Throws<InvalidOperationException>(() => Compute(MasterJsonText, string.Empty, NoiseOrigin, Seed));
        }

        [Test]
        public void ChangesWhenTheMapObjectLayoutChanges()
        {
            // 木の摂動・根元テクスチャ・距離場は配置の派生物。ここを外すと木を1本動かしても古い見た目が残る
            // The tree perturbation, root textures and distance fields all derive from the layout; missing it keeps stale visuals after a single tree moves
            var movedDigest = new byte[MapObjectsDigest.Length];
            movedDigest[0] = 1;

            Assert.That(
                TerrainVisualCacheKey.Compute(MasterJsonText, TerrainHash, NoiseOrigin, Seed, movedDigest),
                Is.Not.EqualTo(Compute(MasterJsonText, TerrainHash, NoiseOrigin, Seed)));
        }

        [Test]
        public void ThrowsWhenTheMapObjectsDigestIsMissing()
        {
            // 空ダイジェストで黙って通すと、配線漏れが「配置を変えてもキャッシュが効き続ける」形でしか現れない
            // Letting an empty digest pass silently would surface a wiring gap only as a cache that survives every layout change
            Assert.Throws<InvalidOperationException>(
                () => TerrainVisualCacheKey.Compute(MasterJsonText, TerrainHash, NoiseOrigin, Seed, null));
            Assert.Throws<InvalidOperationException>(
                () => TerrainVisualCacheKey.Compute(MasterJsonText, TerrainHash, NoiseOrigin, Seed, new byte[0]));
        }

        private static string Compute(string generationMasterJsonText, string terrainHash, Vector2 noiseOrigin, int seed)
        {
            return TerrainVisualCacheKey.Compute(generationMasterJsonText, terrainHash, noiseOrigin, seed, MapObjectsDigest);
        }
    }
}
