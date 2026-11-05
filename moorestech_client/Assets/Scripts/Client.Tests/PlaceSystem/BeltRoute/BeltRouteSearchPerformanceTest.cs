using System.Diagnostics;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Client.Tests.PlaceSystem.BeltRoute
{
    // 本番と同じX64×Z64の箱で、箱の端から端へ障害の中を探索したときの最悪時間を測る（操作感の下限）
    // Measures the worst search time across a production-sized 64x64 box through obstacles from corner to corner (the floor of responsiveness)
    public class BeltRouteSearchPerformanceTest
    {
        private const int Runs = 12;
        private const int HeightCells = 12;

        // 探索と組み立てを合わせた1回の上限。超えたら毎フレーム探索が引っかかる
        // Cap for one search plus recovery; beyond it per-frame searching stutters
        private const double WorstAllowedMilliseconds = 250;

        [Test]
        public void CornerToCornerThroughRandomObstaclesStaysFast()
        {
            var random = new System.Random(7);
            var grid = new BeltRouteGrid(Vector3Int.zero, new Vector3Int(BeltRouteGrid.HorizontalSize, HeightCells, BeltRouteGrid.HorizontalSize));
            for (var x = 0; x < grid.Size.x; x++)
            for (var z = 0; z < grid.Size.z; z++)
                grid.SetColumnFloor(x, z, random.Next(3));
            var cellCount = grid.CellCount / 5;
            for (var i = 0; i < cellCount; i++)
                grid.MarkBlockCell(new Vector3Int(random.Next(grid.Size.x), random.Next(grid.Size.y), random.Next(grid.Size.z)));

            var planner = new BeltRoutePlanner(grid);
            var stopwatch = new Stopwatch();
            var worst = 0.0;
            var found = 0;
            for (var run = 0; run < Runs; run++)
            {
                var start = new Vector3Int(1, 3, 1 + run);
                var end = new Vector3Int(grid.Size.x - 2, 3, grid.Size.z - 2 - run);
                var query = new BeltRouteQuery(start, BlockDirection.East, false, end, BlockDirection.North, true, false, new BeltRouteStepPriority(run));
                stopwatch.Restart();
                var result = planner.Plan(query);
                stopwatch.Stop();
                if (result.HasPlan) found++;
                worst = System.Math.Max(worst, stopwatch.Elapsed.TotalMilliseconds);
            }

            Debug.Log($"[BeltRoutePerformance] worst {worst:F1} ms over {Runs} runs, found {found}");
            Assert.Greater(found, 0, "no route found; the scenario is not measuring a real search");
            Assert.Less(worst, WorstAllowedMilliseconds);
        }
    }
}
