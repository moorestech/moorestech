using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.CleanRoom
{
    // 部屋検出の調停役。設置/破壊で積んだ dirty シードを予算内で消化し、全走査(RebuildAll)と増分更新を world へ反映する。
    // Coordinates room detection: drains place/remove dirty seeds within budget and applies full (RebuildAll) and incremental updates to the world.
    public class CleanRoomDetectionService
    {
        // テスト用: 再検出回数。dirty 制御の検証に使う。
        // Test-only: rebuild counter used to verify dirty gating.
        public int RebuildCount { get; private set; }

        // 直近 tick（または RebuildAll）の fill 訪問セル総数。コスト計測・テスト用。
        // Total fill-visited cells in the last tick (or RebuildAll); for cost measurement/tests.
        public int LastRebuildVisitedCellCount { get; private set; }

        private readonly CleanRoomWorld _world;
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly CleanRoomIncrementalDetector _incremental;

        // 再検出待ちのシードセル。設置/削除の購読で積み、tickで予算内消化する。
        // Seed cells awaiting re-detection; enqueued on place/remove, drained per tick within budget.
        private readonly Queue<Vector3Int> _dirtySeeds = new();
        private readonly HashSet<Vector3Int> _dirtySeedSet = new(); // 重複防止 / dedup

        // 1tickのfill visited予算（バランス確定書§5: 8192）。テストは縮小注入。
        // Per-tick visited budget (balance §5: 8192); tests inject a smaller value.
        private int _dirtyCellBudgetPerTick = CleanRoomDatastore.DirtyCellBudgetPerTick;

        public CleanRoomDetectionService(CleanRoomWorld world, IWorldBlockDatastore worldBlockDatastore)
        {
            _world = world;
            _worldBlockDatastore = worldBlockDatastore;
            _incremental = new CleanRoomIncrementalDetector(world);
        }

        public void SetDirtyCellBudgetPerTick(int budget)
        {
            _dirtyCellBudgetPerTick = budget;
        }

        // 全走査で部屋を再構築する。テスト/ロードから明示的にも呼べる。
        // ロード直後に直接呼ばれた場合も dirty が残らないよう、ここでクリアする。
        // Rebuild all rooms by full scan; clears dirty so a direct load call does not trigger a redundant re-scan next tick.
        public void RebuildAll()
        {
            // 全走査の前に未処理シードを捨てる（次tickでの重複フル走査を防ぐ）。
            // Drop pending seeds before a full scan to avoid a redundant full re-scan next tick.
            _dirtySeeds.Clear();
            _dirtySeedSet.Clear();

            var newRooms = CleanRoomDetector.DetectAllRooms(_worldBlockDatastore, out var visited);
            _world.ReassignRoomIds(newRooms);
            LastRebuildVisitedCellCount = visited;
            ApplyDetectionResult(newRooms); // 引き継ぎ + rooms 全差し替えを一本化
            RebuildCount++;

            #region Internal

            // 全走査の結果反映。旧状態プール＝全 rooms ＋ Degraded 孤立を引き継ぎ、rooms を全差し替え。
            // Apply a full-scan result: pool = all rooms + Degraded orphans; replace rooms entirely.
            void ApplyDetectionResult(List<CleanRoom> detectedRooms)
            {
                var pool = new List<CleanRoom>(_world.Rooms);
                foreach (var orphan in _world.Orphans)
                    if (orphan.Status == CleanRoomRoomStatus.Degraded) pool.Add(orphan);
                _world.Orphans.Clear();

                CleanRoomCarryOver.ApplyCarryOver(detectedRooms, pool, _world.Orphans);
                _world.ReplaceRooms(detectedRooms);
            }

            #endregion
        }

        // dirtyシードを予算内で消化する。最低1シードは必ず処理（前進保証）。
        // Drain dirty seeds within budget; always finish at least one seed per tick.
        public void ProcessDirtySeeds()
        {
            if (_dirtySeeds.Count == 0)
            {
                LastRebuildVisitedCellCount = 0;
                return;
            }

            // 壁/占有セル集合はこの tick の消化で共有（fill 予算とは別。シード単位で作り直さない）。
            // Build cell sets once for this tick's drain (shared across seeds; not the fill budget).
            CleanRoomCellSets.BuildCellSets(_worldBlockDatastore, out var boundaryCells, out var occupiedCells);

            var visitedTotal = 0;
            var processedAny = false;

            while (_dirtySeeds.Count > 0 && (!processedAny || visitedTotal < _dirtyCellBudgetPerTick))
            {
                var seed = _dirtySeeds.Dequeue();
                _dirtySeedSet.Remove(seed);

                // シード周辺を局所fillし、影響部屋の置換/消滅を引き継ぎ規則込みで適用する。
                // Locally fill around the seed and apply room replace/vanish with carry-over rules.
                visitedTotal += _incremental.DetectAroundSeed(seed, boundaryCells, occupiedCells);
                processedAny = true;
            }

            LastRebuildVisitedCellCount = visitedTotal;
        }

        // 設置/破壊で変わったブロックを見て、形状に影響するならシードを積む。
        // On a placed/removed block, enqueue seeds if it can affect room geometry.
        public void OnBlockChanged(WorldBlockData blockData)
        {
            // 境界ブロックは常に部屋形状に影響する。占有セル＋6近傍をシード化。
            // Boundary blocks always affect geometry; enqueue occupied cells + 6-neighbors as seeds.
            if (blockData.Block.TryGetComponent<ICleanRoomBoundaryComponent>(out _))
            {
                EnqueueSeeds(blockData.BlockPositionInfo);
                return;
            }

            // 非境界ブロックも既存部屋の Cells に重なるなら V/S が変わるためシード化。部屋外は無視。
            // Non-boundary blocks overlapping room Cells change V/S, so enqueue them too; ignored when outside any room.
            if (OverlapsAnyRoomCells(blockData.BlockPositionInfo)) EnqueueSeeds(blockData.BlockPositionInfo);

            #region Internal

            bool OverlapsAnyRoomCells(BlockPositionInfo info)
            {
                for (var x = info.MinPos.x; x <= info.MaxPos.x; x++)
                for (var y = info.MinPos.y; y <= info.MaxPos.y; y++)
                for (var z = info.MinPos.z; z <= info.MaxPos.z; z++)
                    if (_world.TryGetCleanRoomAt(new Vector3Int(x, y, z), out _)) return true;
                return false;
            }

            #endregion
        }

        // 変更ブロックの占有セルとその6近傍をシードキューへ積む（重複は HashSet で排除）。
        // Enqueue the changed block's occupied cells and their 6-neighbors (deduped via HashSet).
        private void EnqueueSeeds(BlockPositionInfo info)
        {
            for (var x = info.MinPos.x; x <= info.MaxPos.x; x++)
            for (var y = info.MinPos.y; y <= info.MaxPos.y; y++)
            for (var z = info.MinPos.z; z <= info.MaxPos.z; z++)
            {
                var cell = new Vector3Int(x, y, z);
                Enqueue(cell);
                foreach (var n in CleanRoomCellSets.SixNeighbors(cell)) Enqueue(n);
            }

            #region Internal

            void Enqueue(Vector3Int cell)
            {
                if (_dirtySeedSet.Add(cell)) _dirtySeeds.Enqueue(cell);
            }

            #endregion
        }
    }
}
