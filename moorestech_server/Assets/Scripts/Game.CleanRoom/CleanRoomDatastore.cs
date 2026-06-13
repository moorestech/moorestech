using System;
using System.Collections.Generic;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.World.Interface.DataStore;
using UniRx;
using UnityEngine;

namespace Game.CleanRoom
{
    // クリーンルーム系の中核データストア（GearNetworkDatastore 同型）。
    // フェーズ1は検出のみ。フェーズ2が純度tick・永続化・dirty分割を本クラスに追加する。
    // Core clean-room datastore (same shape as GearNetworkDatastore).
    // Phase 1 is detection only; phase 2 adds the purity tick, persistence and dirty slicing here.
    public class CleanRoomDatastore
    {
        public IReadOnlyList<CleanRoom> Rooms => _rooms;
        // テスト用: 再検出回数。dirty 制御の検証に使う。
        // Test-only: rebuild counter used to verify dirty gating.
        public int RebuildCount { get; private set; }

        private List<CleanRoom> _rooms = new();
        private bool _geometryDirty;
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly List<IDisposable> _subscriptions = new();

        public CleanRoomDatastore(IWorldBlockDatastore worldBlockDatastore)
        {
            _worldBlockDatastore = worldBlockDatastore;

            _subscriptions.Add(GameUpdater.UpdateObservable.Subscribe(_ => Update()));

            // 設置/破壊イベント。remove は block.Destroy() より先に発火するため TryGetComponent は安全。
            // Place/remove events. Remove fires before block.Destroy(), so TryGetComponent is safe.
            _subscriptions.Add(ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(e => OnChanged(e.BlockData)));
            _subscriptions.Add(ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(e => OnChanged(e.BlockData)));

            _geometryDirty = true; // 起動/ロード直後に一度フル検出
        }

        // 全走査で部屋を再構築する。テスト/ロードから明示的にも呼べる。
        // Rebuild all rooms by full scan; callable from tests/load too.
        public void RebuildAll()
        {
            _rooms = CleanRoomDetector.DetectAllRooms(_worldBlockDatastore);
            RebuildCount++;
        }

        public bool TryGetCleanRoomAt(Vector3Int cell, out CleanRoom room)
        {
            foreach (var r in _rooms)
                if (r.Contains(cell)) { room = r; return true; }
            room = null;
            return false;
        }

        public void Destroy()
        {
            foreach (var s in _subscriptions) s.Dispose();
            _subscriptions.Clear();
        }

        private void Update()
        {
            if (!_geometryDirty) return;
            _geometryDirty = false;
            // フェーズ1は全走査。dirty分割・差分更新はフェーズ2が実装する。
            // Phase 1 does a full scan; dirty slicing and diffing land in phase 2.
            RebuildAll();
        }

        private void OnChanged(WorldBlockData blockData)
        {
            // 境界ブロックは常に部屋形状に影響する。
            // Boundary blocks always affect room geometry.
            if (blockData.Block.TryGetComponent<ICleanRoomBoundaryComponent>(out _))
            {
                _geometryDirty = true;
                return;
            }

            // 非境界ブロックも既存部屋の Cells に重なるなら V/S が変わる。
            // Non-boundary blocks overlapping room Cells change V/S.
            // 非境界ブロックを部屋外に置いた場合は次tick以降の壁設置で dirty になる。
            // A non-boundary block outside any room defers dirtying to the next boundary change.
            if (OverlapsAnyRoomCells(blockData.BlockPositionInfo)) _geometryDirty = true;
        }

        private bool OverlapsAnyRoomCells(BlockPositionInfo info)
        {
            for (var x = info.MinPos.x; x <= info.MaxPos.x; x++)
            for (var y = info.MinPos.y; y <= info.MaxPos.y; y++)
            for (var z = info.MinPos.z; z <= info.MaxPos.z; z++)
                if (TryGetCleanRoomAt(new Vector3Int(x, y, z), out _)) return true;
            return false;
        }
    }
}
