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
            Assert.That(sliced[0].LocalPosition.x, Is.EqualTo(250f).Within(1e-3f));
            Assert.That(sliced[0].LocalPosition.z, Is.EqualTo(250f).Within(1e-3f));

            // Yはタイル格子の軸ではない。ここを引くと木の摂動が見ない値を壊しつつ後段の距離場だけが狂う
            // Y is not an axis of the tile lattice; subtracting it would corrupt a value the perturbation ignores while skewing later distance fields
            Assert.That(sliced[0].LocalPosition.y, Is.EqualTo(12f).Within(1e-3f));
            Assert.That(sliced[0].InstanceId, Is.EqualTo(1));
            Assert.That(sliced[0].Guid, Is.EqualTo(MapObjectGuid));
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
            Assert.That(sliced[0].LocalClusterCenter.x, Is.EqualTo(300f).Within(1e-3f));
            Assert.That(sliced[0].LocalClusterCenter.y, Is.EqualTo(300f).Within(1e-3f));
        }

        // 独立配置(-1)の重心は未使用値(0,0)のまま。ここへシフトを適用すると意味の無い非ゼロ値へ化ける
        // An independent placement's (-1) centroid stays the unused (0,0) sentinel; shifting it would turn it into a meaningless non-zero value
        [Test]
        public void LeavesTheIndependentPlacementsClusterCenterAtTheSentinel()
        {
            var sliced = Slice(CreateMapObject(1, 1250f, 0f, -1750f));

            Assert.That(sliced[0].ClusterId, Is.EqualTo(-1));
            Assert.That(sliced[0].LocalClusterCenter.x, Is.EqualTo(0f));
            Assert.That(sliced[0].LocalClusterCenter.y, Is.EqualTo(0f));
        }

        // 姿勢もスケールもタイル格子の軸ではないので素通しする。落とすと切り出した瞬間に向きが消え全配置物が0倍になる
        // Neither rotation nor scale is an axis of the tile lattice; dropping them loses the orientation and zeroes every placement as it is sliced
        [Test]
        public void CarriesTheRotationAndTheScaleThroughUnchanged()
        {
            var scaled = new MapObjectLayoutMessagePack(
                1, MapObjectGuid, 1250f, 0f, -1750f, 0.1f, 0.2f, 0.3f, 0.4f, 1.5f, 2f, 2.5f, -1, 0f, 0f);

            var sliced = Slice(scaled);

            Assert.That(sliced[0].Rotation.x, Is.EqualTo(0.1f));
            Assert.That(sliced[0].Rotation.y, Is.EqualTo(0.2f));
            Assert.That(sliced[0].Rotation.z, Is.EqualTo(0.3f));
            Assert.That(sliced[0].Rotation.w, Is.EqualTo(0.4f));
            Assert.That(sliced[0].Scale.x, Is.EqualTo(1.5f));
            Assert.That(sliced[0].Scale.y, Is.EqualTo(2f));
            Assert.That(sliced[0].Scale.z, Is.EqualTo(2.5f));
        }

        // halo付きの窓は距離場専用。木の摂動が使う半開区間を広げると境界上の1本が両隣で二重に効くため経路を分けてある
        // The haloed window is for distance fields only; widening the half-open one the perturbation uses would double-apply boundary trees
        [Test]
        public void KeepsTheNeighbouringTilesObjectsInsideTheHalo()
        {
            var justOutsideLowerEdge = CreateMapObject(1, TilePosition.x - 30f, 0f, TilePosition.z + 500f);

            Assert.That(Slice(justOutsideLowerEdge).Count, Is.EqualTo(0), "haloなしの窓は隣タイルの木を落とす");

            var haloed = SliceWithHalo(50f, justOutsideLowerEdge);

            Assert.That(haloed.Count, Is.EqualTo(1), "halo内の隣タイルの木は距離場の入力に残る");
            Assert.That(haloed[0].LocalPosition.x, Is.EqualTo(-30f).Within(1e-3f), "タイル外はローカル座標で負値になる");
        }

        [Test]
        public void ExtendsTheWindowByTheHaloOnEveryEdge()
        {
            // 4辺すべてに広げないと、広げ忘れた側の境界だけが距離場の抜けた帯になる
            // Without extending all four edges, the forgotten side alone becomes a band where the distance field is missing
            var haloed = SliceWithHalo(50f,
                CreateMapObject(1, TilePosition.x - 49f, 0f, TilePosition.z + 500f),
                CreateMapObject(2, TilePosition.x + TileSize + 49f, 0f, TilePosition.z + 500f),
                CreateMapObject(3, TilePosition.x + 500f, 0f, TilePosition.z - 49f),
                CreateMapObject(4, TilePosition.x + 500f, 0f, TilePosition.z + TileSize + 49f));

            Assert.That(haloed.Count, Is.EqualTo(4));
        }

        [Test]
        public void DropsObjectsBeyondTheHalo()
        {
            // halo幅は距離フィルタの探索半径そのもの。外側の点は距離場の結果を1画素も変えないので入れない
            // The halo equals the filters' search radius; points beyond it change no pixel of the distance field
            var haloed = SliceWithHalo(50f,
                CreateMapObject(1, TilePosition.x - 51f, 0f, TilePosition.z + 500f),
                CreateMapObject(2, TilePosition.x + 500f, 0f, TilePosition.z + TileSize + 51f));

            Assert.That(haloed.Count, Is.EqualTo(0));
        }

        [Test]
        public void FallsBackToTheBareHalfOpenWindowAtZeroHalo()
        {
            // halo=0は素の半開区間そのもの。ここがずれると距離場を足しただけで配置側の境界も動く
            // A zero halo is the bare half-open interval itself; a discrepancy here would shift the placement boundary merely by adding distance fields
            var onLowerEdge = CreateMapObject(1, TilePosition.x, 0f, TilePosition.z);
            var onUpperEdge = CreateMapObject(2, TilePosition.x + TileSize, 0f, TilePosition.z);

            Assert.That(SliceWithHalo(0f, onLowerEdge).Count, Is.EqualTo(1));
            Assert.That(SliceWithHalo(0f, onUpperEdge).Count, Is.EqualTo(0));
        }

        private static List<TileLocalMapObject> Slice(params MapObjectLayoutMessagePack[] mapObjects)
        {
            return TileMapObjectSlicer.SliceWithHalo(mapObjects, TilePosition, TileSize, TileSize, 0f);
        }

        private static List<TileLocalMapObject> SliceWithHalo(float halo, params MapObjectLayoutMessagePack[] mapObjects)
        {
            return TileMapObjectSlicer.SliceWithHalo(mapObjects, TilePosition, TileSize, TileSize, halo);
        }

        private static MapObjectLayoutMessagePack CreateMapObject(int instanceId, float x, float y, float z)
        {
            return new MapObjectLayoutMessagePack(
                instanceId, MapObjectGuid, x, y, z,
                0f, 0f, 0f, 1f, 1f, 1f, 1f, -1, 0f, 0f);
        }

        private static MapObjectLayoutMessagePack CreateClusteredMapObject(
            int instanceId, float x, float y, float z, int clusterId, float clusterCenterX, float clusterCenterZ)
        {
            return new MapObjectLayoutMessagePack(
                instanceId, MapObjectGuid, x, y, z,
                0f, 0f, 0f, 1f, 1f, 1f, 1f, clusterId, clusterCenterX, clusterCenterZ);
        }
    }
}
