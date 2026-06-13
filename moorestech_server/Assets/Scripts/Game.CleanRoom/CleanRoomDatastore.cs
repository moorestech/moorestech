using System;
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CleanRoom.Machine;
using Game.CleanRoom.Pollution;
using Game.CleanRoom.SaveLoad;
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
        private readonly IWorldBlockDatastore _worldBlockDatastore;

        // 再検出待ちのシードセル。設置/削除の購読で積み、tickで予算内消化する。
        // Seed cells awaiting re-detection; enqueued on place/remove, drained per tick within budget.
        private readonly Queue<Vector3Int> _dirtySeeds = new();
        private readonly HashSet<Vector3Int> _dirtySeedSet = new(); // 重複防止 / dedup

        // 1tickのfill visited予算（バランス確定書§5: 8192）。テストは縮小注入。
        // Per-tick visited budget (balance §5: 8192); tests inject a smaller value.
        public const int DirtyCellBudgetPerTick = 8192;
        private int _dirtyCellBudgetPerTick = DirtyCellBudgetPerTick;

        // 直近 tick（または RebuildAll）の fill 訪問セル総数。コスト計測・テスト用。
        // Total fill-visited cells in the last tick (or RebuildAll); for cost measurement/tests.
        public int LastRebuildVisitedCellCount { get; private set; }

        // 次に生成する部屋の Id。差分更新でも重複しないよう単調増加。
        // Monotonic next room id so incremental detection never reuses an id.
        private int _nextRoomId;

        // どの検出部屋にも紐付かない継続状態（消滅→Degraded/Invalid 中）。猶予で復活待ち。
        // Orphan rooms (vanished -> Degraded/Invalid) awaiting reseal within grace.
        private readonly List<CleanRoom> _orphanRooms = new();
        private readonly List<IDisposable> _subscriptions = new();

        // 汚染レート供給シーム。既定はジオメトリ＋接続点から A_total を算出（フェーズ3配線）。テストは override 可能。
        // Pollution provider seam; defaults to A_total from geometry + connectors (phase-3 wiring). Tests may override.
        private Func<CleanRoom, double> _pollutionPerSecondProvider = DefaultPollutionPerSecond;

        // 既定の汚染レート: 機械数0・ハッチ搬送0で A_total を算出する（フェーズ4/5で実供給）。
        // Default pollution rate: A_total with zero machines and zero hatch throughput (phases 4/5 supply the real values).
        private static double DefaultPollutionPerSecond(CleanRoom room)
        {
            var connectorCount = CleanRoomPollutionCalculator.CountConnectors(room);
            return CleanRoomPollutionCalculator.ComputeATotal(room.Volume, room.SurfaceArea, connectorCount, 0, 0.0);
        }

        // エアフィルター登録（セル→フィルター）。フェーズ3のブロックが設置/破壊時に呼ぶ。
        // Air filter registry (cell -> filter); phase-3 blocks register on place/remove.
        private readonly Dictionary<Vector3Int, ICleanRoomAirFilter> _airFilters = new();

        // 状態受信ブロック登録（BlockInstanceId→ブロック）。設置/破壊で自動登録し、毎tick効果をプッシュする。
        // ブロック参照を持つことで TryGetCleanRoom(block, ..) を再利用し占有セルの帰属判定を委譲できる。
        // State-receiver registry (instance id -> block); auto-registered on place/remove, pushed each tick.
        // Holding the block lets us reuse TryGetCleanRoom(block, ..) for the multi-block membership check.
        private readonly Dictionary<BlockInstanceId, (IBlock block, ICleanRoomStateReceiver receiver)> _stateReceivers = new();

        // 閾値行（マスタから1回変換してキャッシュ）。
        // Threshold rows converted once from the master.
        private IReadOnlyList<CleanRoomThresholdRow> _thresholdRows;

        public CleanRoomDatastore(IWorldBlockDatastore worldBlockDatastore)
        {
            _worldBlockDatastore = worldBlockDatastore;

            _subscriptions.Add(GameUpdater.UpdateObservable.Subscribe(_ => Update()));

            // 設置/破壊イベント。remove は block.Destroy() より先に発火するため TryGetComponent は安全。
            // Place/remove events. Remove fires before block.Destroy(), so TryGetComponent is safe.
            _subscriptions.Add(ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(e => { RegisterAirFilterOnPlace(e.BlockData); RegisterStateReceiverOnPlace(e.BlockData); OnChanged(e.BlockData); }));
            _subscriptions.Add(ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(e => { UnregisterAirFilterOnRemove(e.BlockData); UnregisterStateReceiverOnRemove(e.BlockData); OnChanged(e.BlockData); }));
        }

        public void SetDirtyCellBudgetPerTickForTest(int budget)
        {
            _dirtyCellBudgetPerTick = budget;
        }

        // 全走査で部屋を再構築する。テスト/ロードから明示的にも呼べる。
        // Rebuild all rooms by full scan; callable from tests/load too.
        // ロード直後に直接呼ばれた場合も dirty が残らないよう、ここでクリアする。
        // Clear dirty here so a direct call from load sequence does not trigger a redundant full re-scan on the next tick.
        public void RebuildAll()
        {
            // 全走査の前に未処理シードを捨てる（次tickでの重複フル走査を防ぐ）。
            // Drop pending seeds before a full scan to avoid a redundant full re-scan next tick.
            _dirtySeeds.Clear();
            _dirtySeedSet.Clear();

            var newRooms = CleanRoomDetector.DetectAllRooms(_worldBlockDatastore, out var visited);
            ReassignRoomIds(newRooms);
            LastRebuildVisitedCellCount = visited;
            ApplyDetectionResult(newRooms); // 引き継ぎ + _rooms 全差し替えを一本化
            RebuildCount++;
        }

        // 検出結果に単調増加 Id を振り直す（Detector は 0 起点のため）。
        // Reassign monotonic ids to detected rooms (the detector starts from 0).
        private void ReassignRoomIds(List<CleanRoom> rooms)
        {
            foreach (var room in rooms) room.SetId(_nextRoomId++);
        }

        public bool TryGetDegradedOrphan(out CleanRoom orphan)
        {
            // テスト/フェーズ4用: 最初の孤立状態を返す。
            // For tests/phase 4: return the first orphan.
            orphan = _orphanRooms.Count > 0 ? _orphanRooms[0] : null;
            return orphan != null;
        }

        // 全走査の結果反映。旧状態プール＝全 _rooms ＋ Degraded 孤立を引き継ぎ、_rooms を全差し替え。
        // Apply a full-scan result: pool = all _rooms + Degraded orphans; replace _rooms entirely.
        private void ApplyDetectionResult(List<CleanRoom> newRooms)
        {
            var pool = new List<CleanRoom>(_rooms);
            foreach (var orphan in _orphanRooms)
                if (orphan.Status == CleanRoomRoomStatus.Degraded) pool.Add(orphan);
            _orphanRooms.Clear();

            ApplyCarryOver(newRooms, pool);
            _rooms = newRooms;
        }

        // 新検出部屋へ旧状態プールを引き継ぎ、対応しなかった旧状態は孤立化する（RebuildAll/差分更新の共通核）。
        // Carry old-state pool onto new rooms; unmatched old states become orphans. Shared by full and incremental paths.
        // 呼び出し側が _rooms と _orphanRooms の組み替え（全差し替え or 部分置換）を行う。
        // The caller is responsible for swapping _rooms / pulling orphans (full vs partial).
        private void ApplyCarryOver(List<CleanRoom> newRooms, List<CleanRoom> pool)
        {
            var outIndex = MasterHolder.CleanRoomThresholdMaster.OutThresholdIndex;
            var matched = new HashSet<CleanRoom>();

            foreach (var room in newRooms)
            {
                // 重なる旧状態の寄与を合算し、最大重なりの旧状態から行を引き継ぐ。
                // Sum N contributions; carry the threshold row from the max-overlap old room.
                var carriedN = 0.0;
                CleanRoom best = null;
                var bestOverlap = 0;
                foreach (var old in pool)
                {
                    var overlap = CountOverlap(old.Cells, room);
                    if (overlap <= 0) continue;
                    matched.Add(old);
                    carriedN += CleanRoomPurityRules.RedistributeImpurity(old.ImpurityCount, old.Cells.Count, overlap);
                    if (overlap > bestOverlap) { bestOverlap = overlap; best = old; }
                }

                // 新規部屋は Out で開始。重なりがあれば最大重なり旧部屋の行を引き継ぐ。
                // Fresh rooms get Out; rooms with overlap inherit the best old room's row.
                room.SetThresholdIndex(best != null ? best.ThresholdIndex : outIndex);
                if (carriedN > 0.0) room.AddImpurity(carriedN);
                room.SetStatus(CleanRoomRoomStatus.Valid, 0.0);
            }

            // どの新部屋にも対応しなかった旧状態は孤立へ: Valid→Degraded＋猶予開始、Degraded→猶予継続。
            // Unmatched old states become orphans: Valid -> Degraded with fresh grace; Degraded keeps its grace.
            foreach (var old in pool)
            {
                if (matched.Contains(old)) continue;
                if (old.Status == CleanRoomRoomStatus.Valid)
                    old.SetStatus(CleanRoomRoomStatus.Degraded, CleanRoomPurityRules.GraceSeconds);
                _orphanRooms.Add(old);
            }
        }

        // 旧状態のセル集合と新部屋の重なりセル数。
        // Overlapping cell count between an old room and a new room.
        private static int CountOverlap(IReadOnlyCollection<Vector3Int> oldCells, CleanRoom room)
        {
            var count = 0;
            foreach (var cell in oldCells)
                if (room.Contains(cell)) count++;
            return count;
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

        // 設置イベントで実エアフィルターをレジストリへ登録する（1×1×1 なので MinPos がセルキー）。
        // Register a real air filter from the place event (1x1x1, so MinPos is the cell key).
        private void RegisterAirFilterOnPlace(WorldBlockData blockData)
        {
            if (blockData.Block.TryGetComponent<ICleanRoomAirFilter>(out var filter))
                AddAirFilter(blockData.BlockPositionInfo.MinPos, filter);
        }

        // 破壊イベントでレジストリから解除する。
        // Unregister from the registry on the remove event.
        private void UnregisterAirFilterOnRemove(WorldBlockData blockData)
        {
            if (blockData.Block.TryGetComponent<ICleanRoomAirFilter>(out _))
                RemoveAirFilter(blockData.BlockPositionInfo.MinPos);
        }

        // 設置イベントで状態受信ブロックを登録する（multi-block 占有判定用にブロック参照ごと保持）。
        // Register a state-receiver block from the place event (hold the block ref for the membership check).
        private void RegisterStateReceiverOnPlace(WorldBlockData blockData)
        {
            if (blockData.Block.TryGetComponent<ICleanRoomStateReceiver>(out var receiver))
                _stateReceivers[blockData.Block.BlockInstanceId] = (blockData.Block, receiver);
        }

        // 破壊イベントで状態受信ブロックを解除する。
        // Unregister a state-receiver block on the remove event.
        private void UnregisterStateReceiverOnRemove(WorldBlockData blockData)
        {
            if (blockData.Block.TryGetComponent<ICleanRoomStateReceiver>(out _))
                _stateReceivers.Remove(blockData.Block.BlockInstanceId);
        }

        // 検出中の全部屋＋Degraded孤立を保存する（Invalid孤立は保存しない）。
        // Save all detected rooms plus Degraded orphans (Invalid orphans are not saved).
        public List<CleanRoomSaveData> GetSaveData()
        {
            var result = new List<CleanRoomSaveData>();
            foreach (var room in _rooms) result.Add(ToSaveData(room));
            foreach (var orphan in _orphanRooms)
                if (orphan.Status == CleanRoomRoomStatus.Degraded) result.Add(ToSaveData(orphan));
            return result;
        }

        // 再検出済みの部屋へ最大セル重なりで照合して復元する。複数レコード同部屋は N 合算。
        // Restore by max cell overlap; multiple records on one room sum their N.
        public void Restore(IReadOnlyList<CleanRoomSaveData> saveData)
        {
            if (saveData == null) return;

            // 部屋ごとの最大重なりレコードを記録しつつ N を合算する。
            // Track the max-overlap record per room while summing N.
            var bestByRoom = new Dictionary<CleanRoom, (int overlap, CleanRoomSaveData record)>();

            foreach (var record in saveData)
            {
                if (record?.Cells == null) continue;
                var recordCells = ParseCells(record.Cells);

                CleanRoom best = null;
                var bestOverlap = 0;
                foreach (var room in _rooms)
                {
                    var overlap = 0;
                    foreach (var cell in recordCells)
                        if (room.Contains(cell)) overlap++;
                    if (overlap > bestOverlap) { bestOverlap = overlap; best = room; }
                }

                if (best == null)
                {
                    // 未マッチ: Degraded レコードだけ孤立状態として復元（猶予継続）。他は破棄。
                    // Unmatched: only Degraded records become orphans (grace keeps running).
                    if ((CleanRoomRoomStatus)record.Status == CleanRoomRoomStatus.Degraded)
                        _orphanRooms.Add(CreateOrphanFromRecord(record, recordCells));
                    continue;
                }

                best.AddImpurity(record.ImpurityCount); // 合算（後勝ち上書き禁止）
                if (!bestByRoom.TryGetValue(best, out var current) || bestOverlap > current.overlap)
                    bestByRoom[best] = (bestOverlap, record);
            }

            // 行・状態・猶予は最大重なりレコードを採用。
            // Threshold row / status / grace come from the max-overlap record.
            foreach (var kvp in bestByRoom)
            {
                kvp.Key.SetThresholdIndex(kvp.Value.record.ThresholdIndex);
                kvp.Key.SetStatus((CleanRoomRoomStatus)kvp.Value.record.Status, kvp.Value.record.GraceRemainingSeconds);
            }
        }

        // CleanRoom → CleanRoomSaveData へ変換する。
        // Convert a CleanRoom to its save record.
        private static CleanRoomSaveData ToSaveData(CleanRoom room)
        {
            var cells = new List<int[]>();
            foreach (var c in room.Cells)
                cells.Add(new[] { c.x, c.y, c.z });
            return new CleanRoomSaveData
            {
                ImpurityCount = room.ImpurityCount,
                ThresholdIndex = room.ThresholdIndex,
                Status = (int)room.Status,
                GraceRemainingSeconds = (float)room.GraceRemainingSeconds,
                Cells = cells,
            };
        }

        // セルリスト（int[]）を HashSet<Vector3Int> へ変換する。
        // Convert the cell list (int[]) back to a HashSet<Vector3Int>.
        private static HashSet<Vector3Int> ParseCells(List<int[]> cells)
        {
            var set = new HashSet<Vector3Int>(cells.Count);
            foreach (var c in cells)
                if (c != null && c.Length >= 3) set.Add(new Vector3Int(c[0], c[1], c[2]));
            return set;
        }

        // 未マッチ Degraded レコードから孤立 CleanRoom を生成する（猶予継続用）。
        // Create an orphan CleanRoom from an unmatched Degraded record (grace keeps running).
        private CleanRoom CreateOrphanFromRecord(CleanRoomSaveData record, HashSet<Vector3Int> recordCells)
        {
            // 孤立中は純度tick対象外なので V/S=0 で良い。再封時に検出が正値で作り直す。
            // Volume=cells.Count / SurfaceArea=0 is fine since orphans are not purity-ticked.
            var orphan = new CleanRoom(_nextRoomId++, recordCells, recordCells.Count, 0);
            orphan.AddImpurity(record.ImpurityCount);
            orphan.SetThresholdIndex(record.ThresholdIndex);
            orphan.SetStatus(CleanRoomRoomStatus.Degraded, record.GraceRemainingSeconds);
            return orphan;
        }

        public void Destroy()
        {
            foreach (var s in _subscriptions) s.Dispose();
            _subscriptions.Clear();
        }

        private void Update()
        {
            // dirty シードを予算内で消化し、部屋集合を確定してから純度tickを積分する。
            // Drain dirty seeds within budget to finalize the room set, then integrate purity.
            ProcessDirtySeeds();
            UpdatePurity();

            // 純度確定後に、登録済み受信ブロックへ部屋効果をプッシュする（機械側に部屋探索を持たせない）。
            // After purity settles, push room effects to registered receivers (machines never search for rooms).
            PushCleanRoomEffects();
        }

        // 登録済み受信ブロックへ、属する部屋の効果（無ければ最悪側）を毎tickプッシュする。
        // Push each registered receiver the effect of its owning room, or the worst case if it has none.
        private void PushCleanRoomEffects()
        {
            // 同一部屋に全占有セルが含まれる時だけ部屋効果を、またがり/部屋外/無効化時は最悪側をプッシュ。
            // Push the room effect only when all occupied cells lie in one room; straddling/outside/vanished -> worst case.
            foreach (var entry in _stateReceivers.Values)
            {
                if (TryGetCleanRoom(entry.block, out var room))
                    entry.receiver.SetCleanRoomEffect(CleanRoomEffectResolver.Resolve(room));
                else
                    entry.receiver.SetCleanRoomEffect(new CleanRoomEffect(false, 0, 0.0));
            }
        }

        // dirtyシードを予算内で消化する。最低1シードは必ず処理（前進保証）。
        // Drain dirty seeds within budget; always finish at least one seed per tick.
        private void ProcessDirtySeeds()
        {
            if (_dirtySeeds.Count == 0)
            {
                LastRebuildVisitedCellCount = 0;
                return;
            }

            // 壁/占有セル集合はこの tick の消化で共有（fill 予算とは別。シード単位で作り直さない）。
            // Build cell sets once for this tick's drain (shared across seeds; not the fill budget).
            CleanRoomDetector.BuildCellSets(_worldBlockDatastore, out var boundaryCells, out var occupiedCells);

            var visitedTotal = 0;
            var processedAny = false;

            while (_dirtySeeds.Count > 0 && (!processedAny || visitedTotal < _dirtyCellBudgetPerTick))
            {
                var seed = _dirtySeeds.Dequeue();
                _dirtySeedSet.Remove(seed);

                // シード周辺を局所fillし、影響部屋の置換/消滅を引き継ぎ規則込みで適用する。
                // Locally fill around the seed and apply room replace/vanish with carry-over rules.
                visitedTotal += DetectAroundSeed(seed, boundaryCells, occupiedCells);
                processedAny = true;
            }

            LastRebuildVisitedCellCount = visitedTotal;
        }

        // シード周辺を局所 fill し、影響を受ける既存部屋だけを差分更新する。fill 訪問セル数を返す。
        // Locally fill around the seed and differentially update only the affected rooms. Returns visited cells.
        private int DetectAroundSeed(Vector3Int seed, HashSet<Vector3Int> boundaryCells, HashSet<Vector3Int> occupiedCells)
        {
            // 局所 fill で新たな密閉部屋を検出（触れた壁AABB+1・MaxRoomVolume で縛る）。
            // Detect new sealed rooms by local fill (bounded by touched-wall AABB+1 / MaxRoomVolume).
            var seeds = new List<Vector3Int> { seed };
            var newRooms = CleanRoomDetector.DetectFromSeeds(seeds, boundaryCells, occupiedCells, 0, out var visited);

            // 影響対象＝シード近傍に重なる既存部屋 ＋ 新部屋セルに重なる既存部屋。
            // Affected rooms = existing rooms overlapping the seed neighborhood or any new-room cell.
            var probe = ProbeRegion(seed);
            var affected = CollectAffectedRooms(probe, newRooms);

            // この領域の再検出で、重なる Invalid 孤立を破棄する（全走査 ApplyDetectionResult と同じ「再検出で破棄」を実現）。
            // Discard Invalid orphans overlapping this re-detected region (mirrors the full-path "discard on re-detection").
            // Invalid はプールに入れない＝引き継がない（新部屋は N=0 開始。汚染の蘇生を防ぐ）。
            // Invalid orphans are NOT pooled, so no carry-over: the new room starts at N=0 (no impurity resurrection).
            DiscardOverlappingInvalidOrphans(probe, newRooms);

            // 新部屋が既存部屋の Cells と完全一致なら何もしない（インスタンス維持）。
            // If a new room exactly matches an existing room's Cells, keep the instance (do nothing).
            RemoveExactMatches(newRooms, affected);
            if (newRooms.Count == 0 && affected.Count == 0) return visited;

            ReassignRoomIds(newRooms);

            // 影響部屋＋それに重なる Degraded 孤立だけをプールにし、引き継ぎ後に _rooms を部分置換する。
            // Pool = affected rooms + overlapping Degraded orphans only; partially replace _rooms after carry-over.
            var pool = new List<CleanRoom>(affected);
            PullOverlappingDegradedOrphans(probe, newRooms, pool);

            ApplyCarryOver(newRooms, pool);

            // 影響部屋を取り除き、新部屋を加える（触れていない部屋はインスタンスごと維持）。
            // Drop affected rooms and add the new ones; untouched rooms keep their instances.
            var next = new List<CleanRoom>(_rooms.Count);
            foreach (var room in _rooms)
                if (!affected.Contains(room)) next.Add(room);
            next.AddRange(newRooms);
            _rooms = next;

            return visited;

            #region Internal

            // シードと6近傍を既存部屋の Cells 重なり判定に使う探索域。
            // Probe region: the seed plus its 6 neighbors, used to find overlapping existing rooms.
            HashSet<Vector3Int> ProbeRegion(Vector3Int s)
            {
                var set = new HashSet<Vector3Int> { s };
                set.Add(new Vector3Int(s.x + 1, s.y, s.z));
                set.Add(new Vector3Int(s.x - 1, s.y, s.z));
                set.Add(new Vector3Int(s.x, s.y + 1, s.z));
                set.Add(new Vector3Int(s.x, s.y - 1, s.z));
                set.Add(new Vector3Int(s.x, s.y, s.z + 1));
                set.Add(new Vector3Int(s.x, s.y, s.z - 1));
                return set;
            }

            #endregion
        }

        // probe セルを含む、または新部屋セルに重なる既存部屋を集める。
        // Collect existing rooms that contain a probe cell or overlap any new-room cell.
        private HashSet<CleanRoom> CollectAffectedRooms(HashSet<Vector3Int> probe, List<CleanRoom> newRooms)
        {
            var affected = new HashSet<CleanRoom>();
            foreach (var room in _rooms)
            {
                if (ContainsAny(room, probe)) { affected.Add(room); continue; }
                foreach (var newRoom in newRooms)
                    if (CountOverlap(room.Cells, newRoom) > 0) { affected.Add(room); break; }
            }
            return affected;
        }

        // probe または新部屋に重なる Degraded 孤立をプールへ移す（猶予中の再密閉対応）。
        // Move Degraded orphans overlapping the probe or a new room into the pool (reseal within grace).
        private void PullOverlappingDegradedOrphans(HashSet<Vector3Int> probe, List<CleanRoom> newRooms, List<CleanRoom> pool)
        {
            for (var i = _orphanRooms.Count - 1; i >= 0; i--)
            {
                var orphan = _orphanRooms[i];
                if (orphan.Status != CleanRoomRoomStatus.Degraded) continue;

                var overlaps = ContainsAny(orphan, probe);
                if (!overlaps)
                    foreach (var newRoom in newRooms)
                        if (CountOverlap(orphan.Cells, newRoom) > 0) { overlaps = true; break; }

                if (!overlaps) continue;
                pool.Add(orphan);
                _orphanRooms.RemoveAt(i);
            }
        }

        // probe または新部屋に重なる Invalid 孤立を破棄する（引き継がず削除のみ＝N蘇生なし）。
        // Discard Invalid orphans overlapping the probe or a new room (removed only, never pooled -> no N resurrection).
        private void DiscardOverlappingInvalidOrphans(HashSet<Vector3Int> probe, List<CleanRoom> newRooms)
        {
            for (var i = _orphanRooms.Count - 1; i >= 0; i--)
            {
                var orphan = _orphanRooms[i];
                if (orphan.Status != CleanRoomRoomStatus.Invalid) continue;

                var overlaps = ContainsAny(orphan, probe);
                if (!overlaps)
                    foreach (var newRoom in newRooms)
                        if (CountOverlap(orphan.Cells, newRoom) > 0) { overlaps = true; break; }

                if (!overlaps) continue;
                _orphanRooms.RemoveAt(i);
            }
        }

        // 新部屋の Cells が影響部屋の Cells と完全一致するなら、両方を処理対象から外す（インスタンス維持）。
        // Drop exact-match pairs from processing so the existing instance is preserved.
        private static void RemoveExactMatches(List<CleanRoom> newRooms, HashSet<CleanRoom> affected)
        {
            for (var i = newRooms.Count - 1; i >= 0; i--)
            {
                CleanRoom exact = null;
                foreach (var room in affected)
                    if (RoomEquivalent(room, newRooms[i])) { exact = room; break; }
                if (exact == null) continue;
                affected.Remove(exact);
                newRooms.RemoveAt(i);
            }
        }

        // Cells・Volume・SurfaceArea が全一致なら同一部屋とみなしインスタンスを維持する。
        // Treat as the same room (preserve instance) only if Cells, Volume, and SurfaceArea all match.
        private static bool RoomEquivalent(CleanRoom a, CleanRoom b)
        {
            if (a.Cells.Count != b.Cells.Count) return false;
            if (a.Volume != b.Volume || a.SurfaceArea != b.SurfaceArea) return false;
            foreach (var cell in b.Cells)
                if (!a.Contains(cell)) return false;
            return true;
        }

        private static bool ContainsAny(CleanRoom room, HashSet<Vector3Int> cells)
        {
            foreach (var cell in cells)
                if (room.Contains(cell)) return true;
            return false;
        }

        // 毎tick: 全部屋の N を積分し、閾値行を二条件＋ヒステリシスで更新する。
        // Each tick: integrate N for every room and update the threshold row.
        private void UpdatePurity()
        {
            EnsureThresholdRows();

            foreach (var room in _rooms)
            {
                var aTotal = _pollutionPerSecondProvider(room);

                // 部屋内フィルターを一度だけ集める（n·q と摩耗配分で共有）。
                // Collect in-room filters once (shared between n·q and wear distribution).
                var filters = CollectAirFilters(room);
                var nq = 0.0;
                foreach (var f in filters) nq += f.RemovalVolumePerSecond;

                // 今tickの除去総量を旧濃度から算出（N をマイナスにしない）。IntegrateTick の除去項と一致。
                // Removed amount this tick from the OLD concentration (never below N); matches IntegrateTick's removal term.
                var removedTotal = nq * room.Concentration * GameUpdater.SecondsPerTick;
                if (removedTotal > room.ImpurityCount) removedTotal = room.ImpurityCount;

                // dN 積分（0クランプは純関数内）。
                // Integrate dN (zero clamp inside the pure function).
                var newN = CleanRoomPurityRules.IntegrateTick(room.ImpurityCount, room.Volume, aTotal, nq, GameUpdater.SecondsPerTick);
                var delta = newN - room.ImpurityCount;
                if (delta >= 0.0) room.AddImpurity(delta);
                else room.RemoveImpurity(-delta);

                // 除去寄与をフィルターへ配分（汚染レート比例の摩耗）。
                // Distribute removed impurity to filters (wear proportional to removal rate).
                if (nq > 0.0 && removedTotal > 0.0)
                    foreach (var f in filters)
                        f.ApplyRemovedImpurity(removedTotal * (f.RemovalVolumePerSecond / nq));

                // 閾値行の更新（ACH = n·q/V）。
                // Update threshold row with ACH = n·q/V.
                var ach = room.Volume > 0 ? nq / room.Volume : 0.0;
                room.SetThresholdIndex(CleanRoomPurityRules.DecideThresholdIndex(room.ThresholdIndex, room.Concentration, ach, _thresholdRows));
            }

            // 孤立状態の猶予を毎tick減らし、切れたら Invalid（破棄は次の再検出時）。
            // Tick down orphan grace; on expiry mark Invalid (discarded at the next re-detection).
            foreach (var orphan in _orphanRooms)
            {
                if (orphan.Status != CleanRoomRoomStatus.Degraded) continue;
                var remaining = orphan.GraceRemainingSeconds - GameUpdater.SecondsPerTick;
                if (remaining > 0.0) orphan.SetStatus(CleanRoomRoomStatus.Degraded, remaining);
                else orphan.SetStatus(CleanRoomRoomStatus.Invalid, 0.0);
            }
        }

        // 部屋に属するフィルターを集める（登録セルが部屋の Cells に含まれるもの）。
        // Collect filters whose registered cell lies in the room's Cells.
        private List<ICleanRoomAirFilter> CollectAirFilters(CleanRoom room)
        {
            var result = new List<ICleanRoomAirFilter>();
            foreach (var kvp in _airFilters)
                if (room.Contains(kvp.Key)) result.Add(kvp.Value);
            return result;
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
            // 境界ブロックは常に部屋形状に影響する。占有セル＋6近傍をシード化。
            // Boundary blocks always affect geometry; enqueue occupied cells + 6-neighbors as seeds.
            if (blockData.Block.TryGetComponent<ICleanRoomBoundaryComponent>(out _))
            {
                EnqueueSeeds(blockData.BlockPositionInfo);
                return;
            }

            // 非境界ブロックも既存部屋の Cells に重なるなら V/S が変わるためシード化。
            // Non-boundary blocks overlapping room Cells change V/S, so enqueue them too.
            // 部屋外の非境界ブロックは無視（フェーズ1のゲーティングを踏襲）。
            // Non-boundary blocks outside any room are ignored (matching phase-1 gating).
            if (OverlapsAnyRoomCells(blockData.BlockPositionInfo)) EnqueueSeeds(blockData.BlockPositionInfo);
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
                foreach (var n in SixNeighbors(cell)) Enqueue(n);
            }

            #region Internal

            void Enqueue(Vector3Int cell)
            {
                if (_dirtySeedSet.Add(cell)) _dirtySeeds.Enqueue(cell);
            }

            #endregion
        }

        private static IEnumerable<Vector3Int> SixNeighbors(Vector3Int p)
        {
            yield return new Vector3Int(p.x + 1, p.y, p.z);
            yield return new Vector3Int(p.x - 1, p.y, p.z);
            yield return new Vector3Int(p.x, p.y + 1, p.z);
            yield return new Vector3Int(p.x, p.y - 1, p.z);
            yield return new Vector3Int(p.x, p.y, p.z + 1);
            yield return new Vector3Int(p.x, p.y, p.z - 1);
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
