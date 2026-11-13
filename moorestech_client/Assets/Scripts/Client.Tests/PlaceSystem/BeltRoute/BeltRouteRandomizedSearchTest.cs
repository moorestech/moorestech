using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search;
using Client.Tests.PlaceSystem.BeltRoute.World;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltRoute
{
    // 種を固定した乱数で障害・既存ベルト・地形を散らし、五十回の探索結果が全て不変条件を満たすことを確かめる（回数は PR 追加テスト全体で 1 秒に収める裁定 2026-11-07 による）
    // Scatters obstacles, existing belts and terrain with a fixed seed and checks that fifty search results all satisfy the invariants (the count keeps the PR's tests within one second in total, user decision 2026-11-07)
    public class BeltRouteRandomizedSearchTest
    {
        private const int Iterations = 50;
        private const int Seed = 20261106;

        [Test]
        public void RandomQueriesProduceOnlyValidRoutes()
        {
            var random = new System.Random(Seed);
            var found = 0;
            for (var i = 0; i < Iterations; i++)
            {
                var grid = BuildRandomGrid(random, new Vector3Int(20, 8, 20));
                var planner = new BeltRoutePlanner(grid, BeltRoutePlanner.NoRecoverySlack);
                if (!TryPickQuery(random, grid, out var query)) continue;

                var result = planner.Plan(query);
                if (!result.HasPlan) continue;
                found++;
                BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, $"iteration {i} query {query.StartCell}:{query.StartDirection} -> {query.EndCell}:{query.EndDirection} slopes:{query.AllowSlopes} splitter:{query.TurnByOutput} branch:{query.StartIsExistingBelt}");
            }
            Debug.Log($"[BeltRouteRandomized] found {found} routes in {Iterations} iterations");
            Assert.Greater(found, Iterations / 4, "too few routes found; the random scenario is not exercising the search");
        }

        // 地形は列ごとに0〜2の下限、既存ブロックは1〜3割、そのうち一部をベルト（出力辺・入力辺・坂の真上）にする
        // Terrain floors of 0 to 2 per column, 10 to 30 percent existing blocks, some of them belts with output/input edges and slope heads
        internal static BeltRouteGrid BuildRandomGrid(System.Random random, Vector3Int size)
        {
            var grid = new BeltRouteGrid(Vector3Int.zero, size);
            for (var x = 0; x < size.x; x++)
            for (var z = 0; z < size.z; z++)
                if (random.Next(10) != 0) grid.SetColumnFloor(x, z, random.Next(3));

            var density = 0.1 + random.NextDouble() * 0.2;
            var cellCount = (int)(size.x * size.y * size.z * density);
            for (var i = 0; i < cellCount; i++)
            {
                var cell = new Vector3Int(random.Next(size.x), random.Next(size.y), random.Next(size.z));
                grid.MarkBlockCell(cell);
                if (random.Next(3) != 0) continue;
                WriteRandomBelt(random, grid, cell);
            }
            return grid;
        }

        private static void WriteRandomBelt(System.Random random, BeltRouteGrid grid, Vector3Int cell)
        {
            var facing = (BlockDirection)random.Next(4);
            var kindRoll = random.Next(5);
            if (kindRoll == 0)
            {
                grid.AddInputEdge(BeltRouteEdge.FromFace(cell, BeltRouteDirection.Opposite(facing), cell.y));
                grid.AddOutputEdge(BeltRouteEdge.FromFace(cell, facing, cell.y + 1));
                grid.MarkSlopeHead(cell + Vector3Int.up);
                return;
            }
            if (kindRoll == 1)
            {
                grid.AddInputEdge(BeltRouteEdge.FromFace(cell, BeltRouteDirection.Opposite(facing), cell.y + 1));
                grid.AddOutputEdge(BeltRouteEdge.FromFace(cell, facing, cell.y));
                grid.MarkSlopeHead(cell + Vector3Int.up);
                return;
            }
            grid.AddOutputEdge(BeltRouteEdge.FromFace(cell, facing, cell.y));
            grid.AddInputEdge(BeltRouteEdge.FromFace(cell, BeltRouteDirection.Opposite(facing), cell.y));
            grid.AddInputEdge(BeltRouteEdge.FromFace(cell, BeltRouteDirection.Left(facing), cell.y));
            grid.AddInputEdge(BeltRouteEdge.FromFace(cell, BeltRouteDirection.Right(facing), cell.y));
        }

        private static bool TryPickQuery(System.Random random, BeltRouteGrid grid, out BeltRouteQuery query)
        {
            query = null;
            if (!TryPickFreeCell(random, grid, out var start) || !TryPickFreeCell(random, grid, out var end) || start == end) return false;

            var startIsExistingBelt = random.Next(10) == 0;
            var turnByOutput = !startIsExistingBelt && random.Next(5) == 0;
            var allowSlopes = !turnByOutput && random.Next(5) != 0;
            var startDirection = (BlockDirection)random.Next(4);
            // 分岐の始点は既存ベルトなので障害として塗る
            // A branch start is an existing belt, so it is painted as an obstacle
            if (startIsExistingBelt) grid.MarkBlockCell(start);
            query = new BeltRouteQuery(start, startDirection, startIsExistingBelt, end, (BlockDirection)random.Next(4), allowSlopes, turnByOutput);
            return true;
        }

        private static bool TryPickFreeCell(System.Random random, BeltRouteGrid grid, out Vector3Int cell)
        {
            for (var attempt = 0; attempt < 50; attempt++)
            {
                cell = grid.Min + new Vector3Int(random.Next(grid.Size.x), random.Next(grid.Size.y), random.Next(grid.Size.z));
                if (!grid.IsBlockedByBlock(cell) && !grid.IsBlockedByTerrain(cell) && !grid.IsSlopeHead(cell)) return true;
            }
            cell = default;
            return false;
        }
    }
}
