using Game.MapGeneration.Pipeline.Generators.Util;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Distance
{
    /// <summary>
    ///     halo付き距離場が乗っているSpatialGridの前提を固定する。タイル外の点はセル添字がクランプされて端セルへ入るが
    ///     座標は真値のまま保たれる、という2点が崩れると距離場は静かに間違った値を返す
    ///     Pins the SpatialGrid assumptions the haloed distance field rides on: out-of-tile points fold into the edge
    ///     cells by clamped index while keeping true coordinates; breaking either makes the field silently wrong
    /// </summary>
    public class SpatialGridHaloAssumptionTest
    {
        private const float TerrainSize = 100f;
        private const float CellSize = 5f;
        private const float MaxRadius = 10f;

        [Test]
        public void MeasuresTheTrueDistanceToAPointBeyondTheLowerEdge()
        {
            // 格納時に座標までクランプしていれば距離は0になる。3であることが真値保持の直接の証拠
            // Clamping the stored coordinate too would yield 0; a 3 is direct evidence the true position survived
            var grid = new SpatialGrid(TerrainSize, TerrainSize, CellSize);
            grid.Add(-3f, 50f);

            Assert.That(grid.FindMinDistance(0f, 50f, MaxRadius), Is.EqualTo(3f).Within(1e-3f));
        }

        [Test]
        public void MeasuresTheTrueDistanceToAPointBeyondTheUpperEdge()
        {
            var grid = new SpatialGrid(TerrainSize, TerrainSize, CellSize);
            grid.Add(TerrainSize + 3f, 50f);

            Assert.That(grid.FindMinDistance(TerrainSize, 50f, MaxRadius), Is.EqualTo(3f).Within(1e-3f));
        }

        [Test]
        public void ReachesAPointFoldedIntoTheCornerCell()
        {
            // 角は2軸ぶんクランプが重なる。走査範囲の計算が片軸だけなら、ここだけが取り逃す
            // A corner folds on both axes at once, so a scan range computed for one axis alone misses only here
            var grid = new SpatialGrid(TerrainSize, TerrainSize, CellSize);
            grid.Add(-3f, -3f);

            Assert.That(grid.FindMinDistance(0f, 0f, MaxRadius), Is.EqualTo(Mathf.Sqrt(18f)).Within(1e-3f));
        }

        [Test]
        public void ReturnsTheCutoffWhenNothingSitsWithinTheRadius()
        {
            // halo外の点まで拾ってしまうと、この対照が崩れて探索半径の打ち切りが効いていないことになる
            // Reaching points outside the halo would break this control and mean the cutoff never applies
            var grid = new SpatialGrid(TerrainSize, TerrainSize, CellSize);
            grid.Add(-30f, 50f);

            Assert.That(grid.FindMinDistance(0f, 50f, MaxRadius), Is.EqualTo(MaxRadius).Within(1e-3f));
        }
    }
}
