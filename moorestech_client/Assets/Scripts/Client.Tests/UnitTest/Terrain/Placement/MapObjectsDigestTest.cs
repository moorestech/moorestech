using System;
using System.Collections.Generic;
using Client.Game.InGame.Environment.Terrain.Build.Placement;
using NUnit.Framework;
using Server.Protocol.PacketResponse.MapData;

namespace Client.Tests.UnitTest.Terrain.Placement
{
    /// <summary>
    ///     見た目キャッシュのキーへ入るMapObjectsダイジェストを検証する。決定的でなければ毎回全タイルを作り直し、
    ///     鈍感であれば動いた配置の見た目を古いまま焼き付ける
    ///     Verifies the MapObjects digest feeding the visual cache key; a non-deterministic one rebuilds every tile each
    ///     run, and an insensitive one bakes in stale visuals for a layout that moved
    /// </summary>
    public class MapObjectsDigestTest
    {
        private const string TreeGuid = "11111111-1111-1111-1111-111111111111";
        private const string RockGuid = "22222222-2222-2222-2222-222222222222";

        [Test]
        public void IgnoresTheOrderTheObjectsArriveIn()
        {
            // サーバーの列挙順が揺れただけで全タイルが取り逃すのを防ぐ。InstanceIdで並べ直してから畳む
            // Stops a shifted server enumeration from missing every tile; the fold happens after reordering by InstanceId
            var ascending = Compute(Create(1, TreeGuid, 10f), Create(2, RockGuid, 20f));
            var descending = Compute(Create(2, RockGuid, 20f), Create(1, TreeGuid, 10f));

            Assert.That(ascending, Is.EqualTo(descending));
        }

        [Test]
        public void ChangesWhenAnObjectMovesOnAnyAxis()
        {
            var baseDigest = Compute(Create(1, TreeGuid, 10f));

            Assert.That(Compute(new MapObjectLayoutMessagePack(1, TreeGuid, 10.5f, 10f, 10f)), Is.Not.EqualTo(baseDigest), "X");
            Assert.That(Compute(new MapObjectLayoutMessagePack(1, TreeGuid, 10f, 10.5f, 10f)), Is.Not.EqualTo(baseDigest), "Y");
            Assert.That(Compute(new MapObjectLayoutMessagePack(1, TreeGuid, 10f, 10f, 10.5f)), Is.Not.EqualTo(baseDigest), "Z");
        }

        [Test]
        public void ChangesWhenAnObjectIsSwappedForAnotherKind()
        {
            // 木と岩は摂動の有無が違う。guidを見落とすと同じ座標のまま木が岩になっても古い地面が残る
            // Trees and rocks differ in whether they perturb; missing the guid would keep the old ground when a tree becomes a rock in place
            Assert.That(Compute(Create(1, RockGuid, 10f)), Is.Not.EqualTo(Compute(Create(1, TreeGuid, 10f))));
        }

        [Test]
        public void ChangesWhenAnObjectIsAddedOrRemoved()
        {
            var single = Compute(Create(1, TreeGuid, 10f));
            var pair = Compute(Create(1, TreeGuid, 10f), Create(2, TreeGuid, 20f));

            Assert.That(pair, Is.Not.EqualTo(single));
        }

        [Test]
        public void SeparatesAdjacentGuidsInsteadOfConcatenatingThem()
        {
            // 長さ無しで連結すると "ab"+"c" と "a"+"bc" が同じ列になる。座標が同じなら区別できなくなる
            // Concatenating without a length would make "ab"+"c" and "a"+"bc" one stream, indistinguishable at equal coordinates
            var split = Compute(
                new MapObjectLayoutMessagePack(1, "ab", 0f, 0f, 0f),
                new MapObjectLayoutMessagePack(2, "c", 0f, 0f, 0f));
            var shifted = Compute(
                new MapObjectLayoutMessagePack(1, "a", 0f, 0f, 0f),
                new MapObjectLayoutMessagePack(2, "bc", 0f, 0f, 0f));

            Assert.That(split, Is.Not.EqualTo(shifted));
        }

        [Test]
        public void FoldsAnEmptyLayoutIntoAHashRatherThanNothing()
        {
            // キー側は空ダイジェストを異常として弾く。mapObjectが0本のワールドを弾かせないため必ず32バイトを返す
            // The key rejects an empty digest as a fault, so a world with zero map objects still gets its 32 bytes
            var digest = MapObjectsDigest.Compute(new List<MapObjectLayoutMessagePack>());

            Assert.That(digest.Length, Is.EqualTo(32));
        }

        private static string Compute(params MapObjectLayoutMessagePack[] mapObjects)
        {
            return BitConverter.ToString(MapObjectsDigest.Compute(mapObjects));
        }

        private static MapObjectLayoutMessagePack Create(int instanceId, string mapObjectGuid, float position)
        {
            return new MapObjectLayoutMessagePack(instanceId, mapObjectGuid, position, position, position);
        }
    }
}
