using System.Diagnostics;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Client.Tests.PlaceSystem.BeltRoute
{
    // 経路が無いときの1回の計画時間を、箱の高さを振って測る。塗りつぶしで弾ける配置と、規則だけで届かず本探索が箱全体を舐める配置の両方
    // Measures one planning call with no route across box heights: ends the flood rejects, and ends only the rules block so the real search sweeps the whole box
    public class BeltRouteNoRoutePerformanceTest
    {
        // 塗りつぶしで弾ける配置は本探索を回さないので、この程度で収まる
        // Floods reject these layouts before the real search, so they stay this small
        private const double FloodRejectAllowedMilliseconds = 30;

        // 本探索が箱全体を舐める最悪。超えるとカーソルを動かしたフレームが引っかかる
        // The worst case where the real search sweeps the whole box; beyond it a cursor-move frame hitches
        private const double FullSweepAllowedMilliseconds = 300;

        [TestCase(6)]
        [TestCase(12)]
        [TestCase(24)]
        public void EnclosedEndIsRejectedByFlood(int heightCells)
        {
            var grid = FlatGrid(heightCells);
            var end = new Vector3Int(40, 2, 40);
            // 終点を既存ブロックの殻で囲う（上下も）
            // Wrap the end in a shell of existing blocks, including above and below
            for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
            for (var dz = -1; dz <= 1; dz++)
                if (dx != 0 || dy != 0 || dz != 0) grid.MarkBlockCell(end + new Vector3Int(dx, dy, dz));

            var worst = Measure(grid, end, "enclosed by blocks", BeltRouteNoPathReason.ExistingBlock);
            Assert.Less(worst, FloodRejectAllowedMilliseconds);
        }

        [TestCase(6)]
        [TestCase(12)]
        [TestCase(24)]
        public void EndBehindTerrainRidgeIsRejectedByFlood(int heightCells)
        {
            var grid = FlatGrid(heightCells);
            // 箱を横切る地形の尾根が上限まで届くので地形だけが障害
            // A terrain ridge across the box reaches the ceiling, so terrain alone blocks the way
            for (var z = 0; z < grid.Size.z; z++) grid.SetColumnFloor(30, z, heightCells);
            var worst = Measure(grid, new Vector3Int(40, 2, 40), "terrain ridge", BeltRouteNoPathReason.Terrain);
            Assert.Less(worst, FloodRejectAllowedMilliseconds);
        }

        [TestCase(6)]
        [TestCase(12)]
        [TestCase(24)]
        public void EndInOpenShaftIsRejectedByFlood(int heightCells)
        {
            var grid = FlatGrid(heightCells);
            var end = new Vector3Int(40, 2, 40);
            // 終点の周りを3段の輪で囲い、真上だけ空ける。ベルトは真下へ降りられないので塗りつぶしも届かない
            // Ring the end three cells tall and leave only the sky open; a belt cannot drop straight down, so the flood does not reach it either
            for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
            for (var dz = -1; dz <= 1; dz++)
                if (dx != 0 || dz != 0) grid.MarkBlockCell(end + new Vector3Int(dx, dy, dz));
            grid.MarkBlockCell(end + Vector3Int.down);

            var worst = Measure(grid, end, "open shaft", BeltRouteNoPathReason.ExistingBlock);
            Assert.Less(worst, FloodRejectAllowedMilliseconds);
        }

        [TestCase(6)]
        [TestCase(12)]
        [TestCase(24)]
        public void EndOnPedestalWithBlockedSlopeHeadsSweepsTheWholeBoxFastEnough(int heightCells)
        {
            var grid = FlatGrid(heightCells);
            var end = new Vector3Int(40, 3, 40);
            // 台座の上の終点を同じ高さの輪で囲う。下から上り坂で入れそうだが坂の真上が輪で塞がるので規則上は届かない
            // The end sits on a pedestal ringed at its own level; up slopes from below look possible, but their heads hit the ring, so the rules reject every entry
            grid.MarkBlockCell(end + Vector3Int.down);
            for (var dx = -1; dx <= 1; dx++)
            for (var dz = -1; dz <= 1; dz++)
                if (dx != 0 || dz != 0) grid.MarkBlockCell(end + new Vector3Int(dx, 0, dz));

            var worst = Measure(grid, end, "pedestal", BeltRouteNoPathReason.Both);
            Assert.Less(worst, FullSweepAllowedMilliseconds);
        }

        private static BeltRouteGrid FlatGrid(int heightCells)
        {
            var grid = new BeltRouteGrid(Vector3Int.zero, new Vector3Int(BeltRouteGrid.HorizontalSize, heightCells, BeltRouteGrid.HorizontalSize));
            for (var x = 0; x < grid.Size.x; x++)
            for (var z = 0; z < grid.Size.z; z++)
                grid.SetColumnFloor(x, z, 2);
            return grid;
        }

        private static double Measure(BeltRouteGrid grid, Vector3Int end, string label, BeltRouteNoPathReason expectedReason)
        {
            var planner = new BeltRoutePlanner(grid);
            var query = new BeltRouteQuery(new Vector3Int(20, 2, 20), BlockDirection.East, false, end, BlockDirection.North, true, false, BeltRouteStepPriority.Default);
            var stopwatch = new Stopwatch();
            var worst = 0.0;
            BeltRoutePlanResult result = null;
            for (var run = 0; run < 3; run++)
            {
                stopwatch.Restart();
                result = planner.Plan(query);
                stopwatch.Stop();
                worst = System.Math.Max(worst, stopwatch.Elapsed.TotalMilliseconds);
            }

            Debug.Log($"[BeltRouteNoRoutePerformance] {label} height {grid.Size.y}: worst {worst:F1} ms, reason {result.NoPathReason}");
            Assert.IsFalse(result.HasPlan);
            Assert.AreEqual(expectedReason, result.NoPathReason);
            return worst;
        }
    }
}
