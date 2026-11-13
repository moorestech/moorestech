using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session
{
    /// <summary>
    ///     始点を決めてから終点を置くまでの状態。探索箱と探索器を持ち、同じ問い合わせの結果は使い回す
    ///     The state between fixing the start and placing the end; owns the search box and planner and reuses results for identical queries
    /// </summary>
    public sealed class BeltRouteSession
    {
        public readonly Vector3Int StartCell;
        public readonly BlockDirection StartDirection;

        // 分岐の始点。通常の始点ではnull
        // The branch start; null for a normal start
        public readonly BeltRouteBranchOrigin Branch;

        private readonly BeltRouteTerrainProfile _terrain;
        private readonly BeltRouteWorldScanner _scanner;
        private readonly BeltRoutePlanner _planner;
        private BeltRouteQuery _lastQuery;
        private BeltRoutePlanResult _lastResult;

        private BeltRouteSession(Vector3Int startCell, BlockDirection startDirection, BeltRouteBranchOrigin branch, BeltRouteTerrainProfile terrain, BeltRouteWorldScanner scanner)
        {
            StartCell = startCell;
            StartDirection = startDirection;
            Branch = branch;
            _terrain = terrain;
            _scanner = scanner;
            _planner = new BeltRoutePlanner(scanner.Build(terrain), BeltRoutePlanner.DefaultRecoverySlack);
        }

        public bool IsBranch => Branch != null;
        public BeltRouteGrid Grid => _planner.Grid;

        // 始点確定時に地形を測って箱を固定する
        // Measures the terrain at start selection and fixes the box
        public static BeltRouteSession CreateAtCell(Vector3Int startCell, BlockDirection startDirection, BeltRouteWorldScanner scanner)
        {
            return new BeltRouteSession(startCell, startDirection, null, BeltRouteTerrainProfile.Scan(startCell), scanner);
        }

        // 分岐は既存ベルトのセルが始点で、向きは選んだ横の出口
        // A branch starts at the existing belt's cell, heading out of the chosen side exit
        public static BeltRouteSession CreateBranch(BeltRouteBranchOrigin branch, BeltRouteWorldScanner scanner)
        {
            return new BeltRouteSession(branch.Cell, branch.SideDirection, branch, BeltRouteTerrainProfile.Scan(branch.Cell), scanner);
        }

        public bool IsNearBox(Vector3Int blockOrigin) => BeltRouteWorldScanner.IsNearBox(Grid, blockOrigin);

        // ブロックの設置・撤去で占有を作り直す。地形は始点確定時のまま
        // Rebuilds occupancy after a block is placed or removed; the terrain stays as measured at start selection
        public void RebuildObstacles()
        {
            _planner.ReplaceGrid(_scanner.Build(_terrain));
            _lastQuery = null;
            _lastResult = null;
        }

        // 作り直しで始点が使えなくなったら始点の選び直しへ戻す
        // When the rebuild invalidates the start, the caller returns to start selection
        public bool IsStartUsable(BlockGameObjectDataStore blocks)
        {
            if (IsBranch)
            {
                return blocks.TryGetBlockGameObject(Branch.Cell, out var current) && current.BlockInstanceId.Equals(Branch.Belt.BlockInstanceId);
            }
            return BeltRouteCellCheck.JudgeInGrid(Grid, StartCell) == BeltRouteCellBlockReason.None;
        }

        // カーソル・向き・優先順が変わったフレームだけ探索する
        // Searches only on frames where the cursor, heading or priority changed
        public BeltRoutePlanResult Plan(Vector3Int endCell, BlockDirection endDirection, BeltRouteStepPriority priority, BeltConveyorHoldingBlock holding)
        {
            var query = new BeltRouteQuery(StartCell, StartDirection, IsBranch, endCell, endDirection, holding.AllowsSlopes, holding.IsSplitter, priority);
            if (_lastQuery != null && _lastQuery.IsSameQuery(query)) return _lastResult;

            _lastQuery = query;
            _lastResult = _planner.Plan(query);
            return _lastResult;
        }
    }
}
