using System;
using System.Collections.Generic;
using Core.Master;
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

        // 汚染レート供給シーム。既定はゼロ。フェーズ3が CleanRoomPollutionCalculator の算出に差し替える。
        // Pollution provider seam; defaults to zero. Phase 3 wires the real calculator here.
        private Func<CleanRoom, double> _pollutionPerSecondProvider = _ => 0.0;

        // エアフィルター登録（セル→フィルター）。フェーズ3のブロックが設置/破壊時に呼ぶ。
        // Air filter registry (cell -> filter); phase-3 blocks register on place/remove.
        private readonly Dictionary<Vector3Int, ICleanRoomAirFilter> _airFilters = new();

        // 閾値行（マスタから1回変換してキャッシュ）。
        // Threshold rows converted once from the master.
        private IReadOnlyList<CleanRoomThresholdRow> _thresholdRows;

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
        // ロード直後に直接呼ばれた場合も dirty が残らないよう、ここでクリアする。
        // Clear dirty here so a direct call from load sequence does not trigger a redundant full re-scan on the next tick.
        public void RebuildAll()
        {
            _geometryDirty = false;
            _rooms = CleanRoomDetector.DetectAllRooms(_worldBlockDatastore);
            RebuildCount++;

            // 新規部屋は Out で開始（保持帯の恩恵を受けない）。引き継ぎがあれば Task 5 で上書き。
            // Fresh rooms start at Out; carry-over (Task 5) overwrites when applicable.
            var outIndex = MasterHolder.CleanRoomThresholdMaster.OutThresholdIndex;
            foreach (var room in _rooms) room.SetThresholdIndex(outIndex);
        }

        public bool TryGetCleanRoomAt(Vector3Int cell, out CleanRoom room)
        {
            foreach (var r in _rooms)
                if (r.Contains(cell)) { room = r; return true; }
            room = null;
            return false;
        }

        // ブロックの全占有セルが同一部屋の Cells に含まれるとき true。内部ブロック（機械等）の帰属判定用。
        // 境界ブロックのセルは Cells に含まれないため常に false（境界用は後続フェーズの GetAdjacentCleanRooms）。
        // True iff every occupied cell lies in the SAME room's Cells. Boundary blocks always return false.
        public bool TryGetCleanRoom(IBlock block, out CleanRoom room)
        {
            var info = block.BlockPositionInfo;
            CleanRoom found = null;
            for (var x = info.MinPos.x; x <= info.MaxPos.x; x++)
            for (var y = info.MinPos.y; y <= info.MaxPos.y; y++)
            for (var z = info.MinPos.z; z <= info.MaxPos.z; z++)
            {
                if (!TryGetCleanRoomAt(new Vector3Int(x, y, z), out var r)) { room = null; return false; }
                if (found == null) found = r;
                else if (!ReferenceEquals(found, r)) { room = null; return false; } // 別部屋にまたがる / spans two rooms
            }
            room = found;
            return found != null;
        }

        public void SetPollutionPerSecondProvider(Func<CleanRoom, double> provider)
        {
            _pollutionPerSecondProvider = provider;
        }

        public void AddAirFilter(Vector3Int cell, ICleanRoomAirFilter filter)
        {
            _airFilters[cell] = filter;
        }

        public void RemoveAirFilter(Vector3Int cell)
        {
            _airFilters.Remove(cell);
        }

        public void Destroy()
        {
            foreach (var s in _subscriptions) s.Dispose();
            _subscriptions.Clear();
        }

        private void Update()
        {
            // dirty なら再検出し、その後に純度tickを実行（同一tick内で部屋集合を確定してから積分）。
            // Rebuild geometry if dirty, then always integrate purity on every tick.
            if (_geometryDirty) RebuildAll();
            UpdatePurity();
        }

        // 毎tick: 全部屋の N を積分し、閾値行を二条件＋ヒステリシスで更新する。
        // Each tick: integrate N for every room and update the threshold row.
        private void UpdatePurity()
        {
            EnsureThresholdRows();

            foreach (var room in _rooms)
            {
                var aTotal = _pollutionPerSecondProvider(room);
                var nq = SumRemovalVolume(room);

                // dN 積分（0クランプは純関数内）。
                // Integrate dN (zero clamp inside the pure function).
                var newN = CleanRoomPurityRules.IntegrateTick(room.ImpurityCount, room.Volume, aTotal, nq, GameUpdater.SecondsPerTick);
                var delta = newN - room.ImpurityCount;
                if (delta >= 0.0) room.AddImpurity(delta);
                else room.RemoveImpurity(-delta);

                // 閾値行の更新（ACH = n·q/V）。
                // Update threshold row with ACH = n·q/V.
                var ach = room.Volume > 0 ? nq / room.Volume : 0.0;
                room.SetThresholdIndex(CleanRoomPurityRules.DecideThresholdIndex(room.ThresholdIndex, room.Concentration, ach, _thresholdRows));
            }

            // 孤立部屋の猶予減算は Task 5 でここに追加する。
            // Orphan grace ticking is added here in Task 5.
        }

        // 部屋に属するフィルターの q 合算（登録セルが部屋の Cells に含まれるもの）。
        // Sum q of filters whose registered cell lies in the room's Cells.
        private double SumRemovalVolume(CleanRoom room)
        {
            var sum = 0.0;
            foreach (var kvp in _airFilters)
                if (room.Contains(kvp.Key)) sum += kvp.Value.RemovalVolumePerSecond;
            return sum;
        }

        private void EnsureThresholdRows()
        {
            if (_thresholdRows != null) return;

            // マスタ要素 → 判定行へ1回だけ変換（生成型のプロパティ名は実生成結果に合わせる）。
            // Convert master elements to decision rows once.
            var rows = new List<CleanRoomThresholdRow>();
            foreach (var element in MasterHolder.CleanRoomThresholdMaster.Rows)
                rows.Add(new CleanRoomThresholdRow(element.MaxConcentration, element.RequiredAirChangeRate));
            _thresholdRows = rows;
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
