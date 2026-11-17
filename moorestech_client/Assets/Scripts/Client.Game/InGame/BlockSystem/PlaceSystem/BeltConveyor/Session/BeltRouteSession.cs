using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session.Branch;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session
{
    /// <summary>
    ///     始点確定後の状態。設置システムの世界を借りて探索し結果を使い回す
    ///     State after the start is fixed; searches the place system's world and reuses results
    ///     世界は始点確定後に作り直さない。変化は確定時に検査する（ADR 0100）
    ///     The world is not rebuilt after the start is fixed; changes are checked at confirm (ADR 0100)
    /// </summary>
    public sealed class BeltRouteSession
    {
        public readonly Vector3Int StartCell;
        public readonly BlockDirection StartDirection;

        // 分岐の始点。通常の始点ではnull
        // The branch start; null for a normal start
        public readonly BeltRouteBranchOrigin Branch;

        private readonly IBeltRouteWorld _world;
        private readonly BeltRoutePlanner _planner;
        private BeltRouteQuery _lastQuery;
        private BeltRoutePlanResult _lastResult;

        private BeltRouteSession(Vector3Int startCell, BlockDirection startDirection, BeltRouteBranchOrigin branch, IBeltRouteWorld world)
        {
            StartCell = startCell;
            StartDirection = startDirection;
            Branch = branch;
            _world = world;
            _planner = new BeltRoutePlanner(_world, BeltRoutePlanner.DefaultRecoverySlack);
        }

        public bool IsBranch => Branch != null;

        public static BeltRouteSession CreateAtCell(Vector3Int startCell, BlockDirection startDirection, IBeltRouteWorld world)
        {
            return new BeltRouteSession(startCell, startDirection, null, world);
        }

        // 分岐は既存ベルトのセルが始点で、向きは幾何が選んだ出口
        // A branch starts at the existing belt's cell, heading out of the exit the geometry chose
        public static BeltRouteSession CreateBranch(BeltRouteBranchOrigin branch, IBeltRouteWorld world)
        {
            return new BeltRouteSession(branch.Cell, branch.ExitDirection, branch, world);
        }

        // 結果の使い回しを捨てて探索し直す。確定時に世界を読み直した後の再照合用
        // Searches again, dropping the reused result; for the re-check after the world is re-read at confirm
        public BeltRoutePlanResult PlanFresh(Vector3Int endCell, BlockDirection endDirection, BeltConveyorHoldingBlock holding)
        {
            _lastQuery = null;
            return Plan(endCell, endDirection, holding);
        }

        // カーソルか向きが変わったフレームだけ探索する
        // Searches only on frames where the cursor or heading changed
        public BeltRoutePlanResult Plan(Vector3Int endCell, BlockDirection endDirection, BeltConveyorHoldingBlock holding)
        {
            // 終点不可なら探索せず理由を返す
            // Unusable end returns its reason without searching
            var endCellReason = BeltRouteCellJudge.Judge(_world, endCell);
            if (endCellReason != BeltRouteCellBlockReason.None) return BeltRoutePlanResult.EndCellBlocked(endCellReason);

            var query = new BeltRouteQuery(StartCell, StartDirection, IsBranch, endCell, endDirection, holding.AllowsSlopes, holding.IsSplitter);
            if (_lastQuery != null && _lastQuery.IsSameQuery(query)) return _lastResult;

            _lastQuery = query;
            _lastResult = _planner.Plan(query);
            return _lastResult;
        }
    }
}
