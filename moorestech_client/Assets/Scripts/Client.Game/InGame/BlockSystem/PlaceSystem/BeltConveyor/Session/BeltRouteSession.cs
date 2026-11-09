using Client.Game.InGame.Block;
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
    ///     始点確定後の状態。世界と探索器を持ち結果を使い回す
    ///     State after the start is fixed; owns world and planner, reuses results
    /// </summary>
    public sealed class BeltRouteSession
    {
        public readonly Vector3Int StartCell;
        public readonly BlockDirection StartDirection;

        // 分岐の始点。通常の始点ではnull
        // The branch start; null for a normal start
        public readonly BeltRouteBranchOrigin Branch;

        private readonly BeltRouteLiveWorld _world;
        private readonly BeltRoutePlanner _planner;
        private BeltRouteQuery _lastQuery;
        private BeltRoutePlanResult _lastResult;

        private BeltRouteSession(Vector3Int startCell, BlockDirection startDirection, BeltRouteBranchOrigin branch, BlockGameObjectDataStore blocks)
        {
            StartCell = startCell;
            StartDirection = startDirection;
            Branch = branch;
            _world = new BeltRouteLiveWorld(blocks);
            _planner = new BeltRoutePlanner(_world, BeltRoutePlanner.DefaultRecoverySlack);
        }

        public bool IsBranch => Branch != null;

        public static BeltRouteSession CreateAtCell(Vector3Int startCell, BlockDirection startDirection, BlockGameObjectDataStore blocks)
        {
            return new BeltRouteSession(startCell, startDirection, null, blocks);
        }

        // 分岐は既存ベルトのセルが始点で、向きは幾何が選んだ出口
        // A branch starts at the existing belt's cell, heading out of the exit the geometry chose
        public static BeltRouteSession CreateBranch(BeltRouteBranchOrigin branch, BlockGameObjectDataStore blocks)
        {
            return new BeltRouteSession(branch.Cell, branch.ExitDirection, branch, blocks);
        }

        // 設置・撤去で占有と辺を再構築
        // Rebuild occupancy and edges on place or removal
        public void RebuildObstacles()
        {
            _world.Rebuild();
            _lastQuery = null;
            _lastResult = null;
        }

        // 作り直しで始点が使えなくなったら始点の選び直しへ戻す
        // When the rebuild invalidates the start, the caller returns to start selection
        public bool IsStartUsable(BlockGameObjectDataStore blocks, BeltConveyorHoldingBlock holding, bool useAlternateExit)
        {
            if (IsBranch)
            {
                // 同じ個体でも隣接変化で分岐器の向き・出口が変わっていたら使えない
                // Even for the same belt, a changed splitter facing or exit makes the start unusable
                if (!blocks.TryGetBlockGameObject(Branch.Cell, out var current) || !current.BlockInstanceId.Equals(Branch.Belt.BlockInstanceId)) return false;
                var reason = BeltRouteBranchOrigin.Judge(blocks, current, holding, useAlternateExit, out var rejudged);
                return reason == BeltRouteBranchBlockReason.None && rejudged.SplitterFacing == Branch.SplitterFacing && rejudged.ExitDirection == Branch.ExitDirection;
            }
            return BeltRouteCellJudge.Judge(_world, StartCell) == BeltRouteCellBlockReason.None;
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
