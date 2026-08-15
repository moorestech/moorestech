using System.Collections.Generic;
using Client.Game.InGame.Environment.Terrain.Build.Placement;
using NUnit.Framework;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;

namespace Client.Tests.UnitTest.Terrain.Placement
{
    /// <summary>
    ///     シーン絶対座標のMapObjectsを1タイルぶんへ切り出す境界を検証する。取りこぼしも二重取りも例外にはならず、
    ///     タイル境界の木の根元だけが平らなまま、あるいは二重に盛られたまま出荷される
    ///     Verifies the boundary carving one tile out of the scene-absolute MapObjects; both a miss and a double take
    ///     throw nothing and merely ship boundary trees with flat or doubly-raised ground
    /// </summary>
    public class TileMapObjectSlicerTest
    {
        private const float TileSize = 1000f;
        private const string MapObjectGuid = "11111111-1111-1111-1111-111111111111";

        // 5x5格子の index(3,0) 相当。負のZを含む位置を選び、符号での取り違えを露出させる
        // Equivalent to index (3, 0) of a 5x5 grid; a negative Z exposes sign mistakes
        private static readonly Vector3 TilePosition = new(1000f, 0f, -2000f);

        [Test]
        public void RebasesTheKeptObjectsOntoTheTileOrigin()
        {
            var sliced = Slice(CreateMapObject(1, 1250f, 12f, -1750f));

            Assert.That(sliced.Count, Is.EqualTo(1));
            Assert.That(sliced[0].X, Is.EqualTo(250f).Within(1e-3f));
            Assert.That(sliced[0].Z, Is.EqualTo(250f).Within(1e-3f));

            // Yはタイル格子の軸ではない。ここを引くと木の摂動が見ない値を壊しつつ後段の距離場だけが狂う
            // Y is not an axis of the tile lattice; subtracting it would corrupt a value the perturbation ignores while skewing later distance fields
            Assert.That(sliced[0].Y, Is.EqualTo(12f).Within(1e-3f));
            Assert.That(sliced[0].InstanceId, Is.EqualTo(1));
            Assert.That(sliced[0].MapObjectGuid, Is.EqualTo(MapObjectGuid));
        }

        [Test]
        public void KeepsTheLowerEdgeAndDropsTheUpperEdge()
        {
            // 半開区間でないと境界上の1本が両隣で効く。二重の摂動は境界の1画素列にしか出ず気付きにくい
            // Without a half-open interval a boundary object acts on both neighbours; the double perturbation shows only on one boundary pixel column
            var onLowerEdge = Slice(CreateMapObject(1, TilePosition.x, 0f, TilePosition.z));
            var onUpperEdge = Slice(CreateMapObject(2, TilePosition.x + TileSize, 0f, TilePosition.z));

            Assert.That(onLowerEdge.Count, Is.EqualTo(1), "下端は含む");
            Assert.That(onUpperEdge.Count, Is.EqualTo(0), "上端は隣のタイルのもの");
        }

        [Test]
        public void DropsObjectsOnEitherSideOfTheTileOnBothAxes()
        {
            var outside = Slice(
                CreateMapObject(1, TilePosition.x - 0.1f, 0f, TilePosition.z),
                CreateMapObject(2, TilePosition.x, 0f, TilePosition.z - 0.1f),
                CreateMapObject(3, TilePosition.x, 0f, TilePosition.z + TileSize));

            Assert.That(outside.Count, Is.EqualTo(0));
        }

        [Test]
        public void KeepsOnlyTheObjectsStandingOnTheTile()
        {
            // 全25タイルぶんが1つの配列で届く。絞り込みを外すと他タイルの木がこのタイルの地面を持ち上げる
            // All 25 tiles arrive in one array; without the filter another tile's trees would lift this tile's ground
            var sliced = Slice(
                CreateMapObject(1, 1500f, 0f, -1500f),
                CreateMapObject(2, 500f, 0f, -1500f),
                CreateMapObject(3, 1500f, 0f, 500f));

            Assert.That(sliced.Count, Is.EqualTo(1));
            Assert.That(sliced[0].InstanceId, Is.EqualTo(1));
        }

        // クラスタ重心もタイルローカル化する。据え置くと岩の位置だけがタイル座標で重心がシーン座標という別フレームになる
        // The cluster centroid is rebased too; leaving it as-is would pair a tile-local rock with a scene-space centroid
        [Test]
        public void RebasesTheClusterCenterOntoTheTileOrigin()
        {
            var sliced = Slice(CreateClusteredMapObject(1, 1250f, 0f, -1750f, 7, 1300f, -1700f));

            Assert.That(sliced.Count, Is.EqualTo(1));
            Assert.That(sliced[0].ClusterId, Is.EqualTo(7));
            Assert.That(sliced[0].ClusterCenterX, Is.EqualTo(300f).Within(1e-3f));
            Assert.That(sliced[0].ClusterCenterZ, Is.EqualTo(300f).Within(1e-3f));
        }

        // 独立配置(-1)の重心は未使用値(0,0)のまま。ここへシフトを適用すると意味の無い非ゼロ値へ化ける
        // An independent placement's (-1) centroid stays the unused (0,0) sentinel; shifting it would turn it into a meaningless non-zero value
        [Test]
        public void LeavesTheIndependentPlacementsClusterCenterAtTheSentinel()
        {
            var sliced = Slice(CreateMapObject(1, 1250f, 0f, -1750f));

            Assert.That(sliced[0].ClusterId, Is.EqualTo(-1));
            Assert.That(sliced[0].ClusterCenterX, Is.EqualTo(0f));
            Assert.That(sliced[0].ClusterCenterZ, Is.EqualTo(0f));
        }

        // スケールはタイル格子の軸ではないので素通しする。落とすと切り出した瞬間に全配置物が0倍になる
        // The scale is not an axis of the tile lattice and passes straight through; dropping it zeroes every placement the moment it is sliced
        [Test]
        public void CarriesTheScaleThroughUnchanged()
        {
            var scaled = new MapObjectLayoutMessagePack(
                1, MapObjectGuid, 1250f, 0f, -1750f, 1.5f, 2f, 2.5f, -1, 0f, 0f);

            var sliced = Slice(scaled);

            Assert.That(sliced[0].ScaleX, Is.EqualTo(1.5f));
            Assert.That(sliced[0].ScaleY, Is.EqualTo(2f));
            Assert.That(sliced[0].ScaleZ, Is.EqualTo(2.5f));
        }

        private static List<MapObjectLayoutMessagePack> Slice(params MapObjectLayoutMessagePack[] mapObjects)
        {
            return TileMapObjectSlicer.Slice(mapObjects, TilePosition, TileSize, TileSize);
        }

        private static MapObjectLayoutMessagePack CreateMapObject(int instanceId, float x, float y, float z)
        {
            return new MapObjectLayoutMessagePack(
                instanceId, MapObjectGuid, x, y, z,
                1f, 1f, 1f, -1, 0f, 0f);
        }

        private static MapObjectLayoutMessagePack CreateClusteredMapObject(
            int instanceId, float x, float y, float z, int clusterId, float clusterCenterX, float clusterCenterZ)
        {
            return new MapObjectLayoutMessagePack(
                instanceId, MapObjectGuid, x, y, z,
                1f, 1f, 1f, clusterId, clusterCenterX, clusterCenterZ);
        }
    }
}
