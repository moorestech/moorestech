using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search;
using Client.Tests.PlaceSystem.BeltRoute.World;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltRoute.Rule
{
    // 既存の出力口の前を経路が通るときの規則を確かめる（ADR 0102）
    // Checks the rules for a route passing in front of existing outputs (ADR 0102)
    public class BeltRouteExistingOutputTest
    {
        [Test]
        public void CellReceivingExistingOutputIsUsedAndMerges()
        {
            var grid = BeltRoutePlannerBasicTest.FlatGrid(16);
            // (4,0,1)の北向きベルトが(4,0,2)へ流している
            // A north-facing belt at (4,0,1) flows into (4,0,2)
            grid.MarkBlockCell(new Vector3Int(4, 0, 1));
            grid.AddOutputEdge(BeltRouteEdge.FromFace(new Vector3Int(4, 0, 1), BlockDirection.North, 0));
            // 出力口の前を避けないので、迂回せず一直線に通る（そのセルは合流になる。ADR 0102）
            // Outputs are not avoided, so the route runs straight through without a detour (that cell becomes a merge, ADR 0102)
            var query = new BeltRouteQuery(new Vector3Int(2, 0, 2), BlockDirection.East, false, new Vector3Int(7, 0, 2), BlockDirection.East, false);
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.NoRecoverySlack).Plan(query);

            Assert.IsTrue(result.HasPlan);
            BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, "existing output");
            Assert.IsTrue(result.Plan.Cells.Any(cell => cell.Cell == new Vector3Int(4, 0, 2)));
            Assert.AreEqual(6, result.Plan.Cells.Count);
            Assert.AreEqual(6 * BeltRouteCellKindExtension.StraightCost, result.Plan.TotalCost);
        }

        // 出力口が1段下の下り坂へ繋がっているとき、出力口の正面は坂の真上なので経路に使わない。使うと坂への接続を奪う（ADR 0102）
        // When an output feeds a down slope one level below, the cell in front of it is the slope's head and stays unused; using it would steal the slope's connection (ADR 0102)
        [Test]
        public void FrontOfAnOutputFeedingADownSlopeBelowStaysUnused()
        {
            var grid = BeltRoutePlannerBasicTest.FlatGrid(16);
            grid.MarkBlockCell(new Vector3Int(4, 1, 1));
            grid.AddOutputEdge(BeltRouteEdge.FromFace(new Vector3Int(4, 1, 1), BlockDirection.North, 1));
            grid.MarkBlockCell(new Vector3Int(4, 0, 2));
            grid.MarkSlopeHead(new Vector3Int(4, 1, 2));
            var query = new BeltRouteQuery(new Vector3Int(2, 1, 2), BlockDirection.East, false, new Vector3Int(7, 1, 2), BlockDirection.East, false);
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.DefaultRecoverySlack).Plan(query);

            Assert.IsTrue(result.HasPlan);
            BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, "slope head in front of an output");
            Assert.IsFalse(result.Plan.Cells.Any(cell => cell.Cell == new Vector3Int(4, 1, 2)));
        }
    }
}
