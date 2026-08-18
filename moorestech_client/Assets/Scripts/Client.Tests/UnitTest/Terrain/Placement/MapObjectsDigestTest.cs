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

            Assert.That(Compute(Create(1, TreeGuid, 10.5f, 10f, 10f)), Is.Not.EqualTo(baseDigest), "X");
            Assert.That(Compute(Create(1, TreeGuid, 10f, 10.5f, 10f)), Is.Not.EqualTo(baseDigest), "Y");
            Assert.That(Compute(Create(1, TreeGuid, 10f, 10f, 10.5f)), Is.Not.EqualTo(baseDigest), "Z");
        }

        [Test]
        public void ChangesWhenAnObjectIsResizedOnAnyAxis()
        {
            // 岩の大きさが周囲テクスチャの広がりを決める。太っただけの回を外すと痩せていた頃の地面が残る
            // A rock's size drives how far its surround texture spreads; missing a run where it merely grew keeps the ground from when it was thin
            var baseDigest = Compute(Create(1, RockGuid, 10f));

            Assert.That(Compute(CreateScaled(2f, 1f, 1f)), Is.Not.EqualTo(baseDigest), "ScaleX");
            Assert.That(Compute(CreateScaled(1f, 2f, 1f)), Is.Not.EqualTo(baseDigest), "ScaleY");
            Assert.That(Compute(CreateScaled(1f, 1f, 2f)), Is.Not.EqualTo(baseDigest), "ScaleZ");
        }

        [Test]
        public void ChangesWhenAnObjectTurnsOnAnyRotationComponent()
        {
            // 向きは配置物の見た目そのもの。回っただけの回を外すと前の向きのまま焼いた見た目が残る
            // The facing is the placement's look itself; missing a run where it merely turned keeps the visuals baked at the old orientation
            var baseDigest = Compute(Create(1, RockGuid, 10f));

            Assert.That(Compute(CreateRotated(0.5f, 0f, 0f, 1f)), Is.Not.EqualTo(baseDigest), "RotationX");
            Assert.That(Compute(CreateRotated(0f, 0.5f, 0f, 1f)), Is.Not.EqualTo(baseDigest), "RotationY");
            Assert.That(Compute(CreateRotated(0f, 0f, 0.5f, 1f)), Is.Not.EqualTo(baseDigest), "RotationZ");
            Assert.That(Compute(CreateRotated(0f, 0f, 0f, 0.5f)), Is.Not.EqualTo(baseDigest), "RotationW");
        }

        [Test]
        public void OrdersObjectsSharingAnInstanceIdByTheirRotationToo()
        {
            // 全順序に姿勢が無いと、同じInstanceIdで向きだけ違う2本の並びが不安定なSortで揺れる
            // Without the rotation in the total order, two objects sharing an InstanceId and differing only in facing shuffle under the unstable Sort
            var ascending = Compute(CreateRotated(0f, 0f, 0f, 1f), CreateRotated(0f, 0.5f, 0f, 1f));
            var descending = Compute(CreateRotated(0f, 0.5f, 0f, 1f), CreateRotated(0f, 0f, 0f, 1f));

            Assert.That(ascending, Is.EqualTo(descending));
        }

        [Test]
        public void ChangesWhenAnObjectJoinsAnotherClusterOrItsCenterMoves()
        {
            // 周囲テクスチャはクラスタ単位に重心から伸びる。所属や重心の変化を外すと隣のクラスタの形が残る
            // The surround texture stretches from a cluster's centroid, so missing a changed membership or centroid keeps the neighbouring shape
            var baseDigest = Compute(Create(1, RockGuid, 10f));

            Assert.That(Compute(CreateClustered(2, 0f, 0f)), Is.Not.EqualTo(baseDigest), "ClusterId");
            Assert.That(Compute(CreateClustered(-1, 5f, 0f)), Is.Not.EqualTo(baseDigest), "ClusterCenterX");
            Assert.That(Compute(CreateClustered(-1, 0f, 5f)), Is.Not.EqualTo(baseDigest), "ClusterCenterZ");
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
                Create(1, "ab", 0f, 0f, 0f),
                Create(2, "c", 0f, 0f, 0f));
            var shifted = Compute(
                Create(1, "a", 0f, 0f, 0f),
                Create(2, "bc", 0f, 0f, 0f));

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
            return Create(instanceId, mapObjectGuid, position, position, position);
        }

        private static MapObjectLayoutMessagePack Create(int instanceId, string mapObjectGuid, float x, float y, float z)
        {
            return new MapObjectLayoutMessagePack(
                instanceId, mapObjectGuid, x, y, z,
                0f, 0f, 0f, 1f, 1f, 1f, 1f, -1, 0f, 0f);
        }

        // Create(1, RockGuid, 10f) と1軸だけ違う岩。差分がスケールだけになるよう他の値は揃える
        // A rock differing from Create(1, RockGuid, 10f) on one axis only, with every other value held equal
        private static MapObjectLayoutMessagePack CreateScaled(float scaleX, float scaleY, float scaleZ)
        {
            return new MapObjectLayoutMessagePack(
                1, RockGuid, 10f, 10f, 10f,
                0f, 0f, 0f, 1f, scaleX, scaleY, scaleZ, -1, 0f, 0f);
        }

        // Create(1, RockGuid, 10f) と姿勢だけ違う岩。差分が向きだけになるよう他の値は揃える
        // A rock differing from Create(1, RockGuid, 10f) in facing alone, with every other value held equal
        private static MapObjectLayoutMessagePack CreateRotated(
            float rotationX, float rotationY, float rotationZ, float rotationW)
        {
            return new MapObjectLayoutMessagePack(
                1, RockGuid, 10f, 10f, 10f,
                rotationX, rotationY, rotationZ, rotationW, 1f, 1f, 1f, -1, 0f, 0f);
        }

        private static MapObjectLayoutMessagePack CreateClustered(int clusterId, float centerX, float centerZ)
        {
            return new MapObjectLayoutMessagePack(
                1, RockGuid, 10f, 10f, 10f,
                0f, 0f, 0f, 1f, 1f, 1f, 1f, clusterId, centerX, centerZ);
        }
    }
}
