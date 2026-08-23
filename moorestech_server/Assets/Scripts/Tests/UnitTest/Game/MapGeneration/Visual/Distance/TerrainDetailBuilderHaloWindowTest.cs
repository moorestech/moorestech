using System.Collections.Generic;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Distance
{
    /// <summary>
    ///     距離場の入力を切り出すhalo窓が探索半径と同じ幅を4辺で持つかを検証する。
    ///     窓が半径より狭いと境界から数十mの帯だけ「近くに木が無い」と誤判定され、その帯の草だけが静かに消える
    ///     Verifies the halo window slicing the distance-field input spans the full search radius on all four edges.
    ///     A narrower window misreads a tens-of-metres band along the seam as tree-free and silently clears its grass
    /// </summary>
    public class TerrainDetailBuilderHaloWindowTest
    {
        private const int MaxDensity = DistanceFieldTestScene.MaxDensity;
        private const int LastPixel = DistanceFieldTestScene.DetailResolution - 1;

        // 探索半径の内外を1mだけ跨がせる。窓と打ち切りが同じ値でなければどちらかの主張が破れる
        // Straddles the search radius by one metre, so any gap between the window and the cutoff breaks one of the claims
        private const float JustInsideRadius = DistanceFieldTestScene.SearchRadius - 1f;
        private const float JustBeyondRadius = DistanceFieldTestScene.SearchRadius + 1f;

        [SetUp]
        public void SetUp()
        {
            DistanceFieldTestScene.LoadMasterData();
        }

        [Test]
        public void SeesTheNeighbouringTilesTreeThroughTheHalo()
        {
            // haloが無ければタイル外の木は入力から丸ごと消え、境界画素だけが「近くに木が無い」と誤判定する
            // Without the halo the out-of-tile tree vanishes from the input and the edge pixel alone misreads as tree-free
            var withNeighbourTree = BuildWithTree(localX: -2f, localZ: 0f);

            Assert.That(withNeighbourTree[0][0, 0], Is.EqualTo(0), "境界の外2mの木は境界画素へ届く");

            // 帯の内側の画素が通ること自体が「木が入力に入った」証拠。木ゼロなら打ち切り距離で埋まりここも落ちる
            // The in-band pixel passing is itself proof the tree entered the input; a tree-free tile saturates and drops it too
            Assert.That(withNeighbourTree[0][2, 2], Is.EqualTo(MaxDensity), "帯の内側の画素は残る");
        }

        [Test]
        public void SeesATreeJustInsideTheSearchRadiusOnEveryEdge()
        {
            Assert.That(EastEdgePixelWithTreeOutside(JustInsideRadius), Is.EqualTo(MaxDensity), "東の探索半径の内側の木が届く");
            Assert.That(WestEdgePixelWithTreeOutside(JustInsideRadius), Is.EqualTo(MaxDensity), "西の探索半径の内側の木が届く");
            Assert.That(NorthEdgePixelWithTreeOutside(JustInsideRadius), Is.EqualTo(MaxDensity), "北の探索半径の内側の木が届く");
            Assert.That(SouthEdgePixelWithTreeOutside(JustInsideRadius), Is.EqualTo(MaxDensity), "南の探索半径の内側の木が届く");
        }

        [Test]
        public void IgnoresATreeJustBeyondTheSearchRadiusOnEveryEdge()
        {
            Assert.That(EastEdgePixelWithTreeOutside(JustBeyondRadius), Is.EqualTo(0), "東の探索半径の外の木は届かない");
            Assert.That(WestEdgePixelWithTreeOutside(JustBeyondRadius), Is.EqualTo(0), "西の探索半径の外の木は届かない");
            Assert.That(NorthEdgePixelWithTreeOutside(JustBeyondRadius), Is.EqualTo(0), "北の探索半径の外の木は届かない");
            Assert.That(SouthEdgePixelWithTreeOutside(JustBeyondRadius), Is.EqualTo(0), "南の探索半径の外の木は届かない");
        }

        // 各辺の端画素と、その画素からちょうどdistanceだけ外側に離れた木。距離が期待値そのものになるよう同じ軸上へ並べる
        // Each edge's end pixel plus a tree exactly distance metres outside it, aligned on one axis so the distance is the expected value
        private static int EastEdgePixelWithTreeOutside(float distance)
        {
            var maps = BuildWithTree(
                DistanceFieldTestScene.PixelWorldCoordinate(LastPixel) + distance, DistanceFieldTestScene.PixelWorldCoordinate(0));
            return maps[0][0, LastPixel];
        }

        private static int WestEdgePixelWithTreeOutside(float distance)
        {
            var maps = BuildWithTree(
                DistanceFieldTestScene.PixelWorldCoordinate(0) - distance, DistanceFieldTestScene.PixelWorldCoordinate(0));
            return maps[0][0, 0];
        }

        private static int NorthEdgePixelWithTreeOutside(float distance)
        {
            var maps = BuildWithTree(
                DistanceFieldTestScene.PixelWorldCoordinate(0), DistanceFieldTestScene.PixelWorldCoordinate(LastPixel) + distance);
            return maps[0][LastPixel, 0];
        }

        private static int SouthEdgePixelWithTreeOutside(float distance)
        {
            var maps = BuildWithTree(
                DistanceFieldTestScene.PixelWorldCoordinate(0), DistanceFieldTestScene.PixelWorldCoordinate(0) - distance);
            return maps[0][0, 0];
        }

        private static List<int[,]> BuildWithTree(float localX, float localZ)
        {
            return DistanceFieldTestScene.Build(
                DistanceFieldTestScene.TreeDistanceSections(),
                DistanceFieldTestScene.CreateMapObject(DistanceFieldTestScene.TreeGuid, localX, localZ));
        }
    }
}
