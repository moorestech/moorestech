using System;
using System.Collections.Generic;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.CleanRoom.SaveLoad;
using Game.Context;
using Game.World.Interface.DataStore;
using UniRx;
using UnityEngine;

namespace Game.CleanRoom
{
    // クリーンルーム系の中核データストア（GearNetworkDatastore 同型）。
    // 状態(CleanRoomWorld)を、検出/純度/登録簿/保存の各サービスへ委譲して回す薄いファサード。
    // Core clean-room datastore (same shape as GearNetworkDatastore).
    // A thin facade that drives the shared CleanRoomWorld through the detection / purity / registry / save services.
    public class CleanRoomDatastore
    {
        // 1tickのfill visited予算（バランス確定書§5: 8192）。テストは縮小注入。
        // Per-tick visited budget (balance §5: 8192); tests inject a smaller value.
        public const int DirtyCellBudgetPerTick = 8192;

        public IReadOnlyList<CleanRoom> Rooms => _world.Rooms;
        public int RebuildCount => _detection.RebuildCount;
        public int LastRebuildVisitedCellCount => _detection.LastRebuildVisitedCellCount;

        private readonly CleanRoomWorld _world = new();
        private readonly CleanRoomBlockRegistries _registries = new();
        private readonly CleanRoomDetectionService _detection;
        private readonly CleanRoomPurityTicker _purityTicker;
        private readonly List<IDisposable> _subscriptions = new();

        public CleanRoomDatastore(IWorldBlockDatastore worldBlockDatastore)
        {
            _detection = new CleanRoomDetectionService(_world, worldBlockDatastore);
            _purityTicker = new CleanRoomPurityTicker(worldBlockDatastore, _registries);

            _subscriptions.Add(GameUpdater.UpdateObservable.Subscribe(_ => Update()));

            // 設置/破壊イベント。remove は block.Destroy() より先に発火するため TryGetComponent は安全。
            // Place/remove events. Remove fires before block.Destroy(), so TryGetComponent is safe.
            _subscriptions.Add(ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(e =>
            {
                _registries.RegisterAirFilterOnPlace(e.BlockData);
                _registries.RegisterStateReceiverOnPlace(e.BlockData);
                _detection.OnBlockChanged(e.BlockData);
            }));
            _subscriptions.Add(ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(e =>
            {
                _registries.UnregisterAirFilterOnRemove(e.BlockData);
                _registries.UnregisterStateReceiverOnRemove(e.BlockData);
                _detection.OnBlockChanged(e.BlockData);
            }));
        }

        private void Update()
        {
            // dirty シードを予算内で消化し、部屋集合を確定してから純度tickを積分する。
            // Drain dirty seeds within budget to finalize the room set, then integrate purity.
            _detection.ProcessDirtySeeds();
            _purityTicker.Tick(_world);

            // 純度確定後に、登録済み受信ブロックへ部屋効果をプッシュする（機械側に部屋探索を持たせない）。
            // After purity settles, push room effects to registered receivers (machines never search for rooms).
            _registries.PushCleanRoomEffects(_world);
        }

        public void SetDirtyCellBudgetPerTickForTest(int budget)
        {
            _detection.SetDirtyCellBudgetPerTick(budget);
        }

        public void RebuildAll()
        {
            _detection.RebuildAll();
        }

        public bool TryGetDegradedOrphan(out CleanRoom orphan)
        {
            // テスト/フェーズ4用: 最初の孤立状態を返す。
            // For tests/phase 4: return the first orphan.
            orphan = _world.Orphans.Count > 0 ? _world.Orphans[0] : null;
            return orphan != null;
        }

        public bool TryGetCleanRoomAt(Vector3Int cell, out CleanRoom room)
        {
            return _world.TryGetCleanRoomAt(cell, out room);
        }

        public bool TryGetCleanRoom(IBlock block, out CleanRoom room)
        {
            return _world.TryGetCleanRoom(block, out room);
        }

        public IReadOnlyList<CleanRoom> GetAdjacentCleanRooms(IBlock boundaryBlock)
        {
            return _world.GetAdjacentCleanRooms(boundaryBlock);
        }

        public void SetPollutionPerSecondProvider(Func<CleanRoom, double> provider)
        {
            _purityTicker.SetPollutionPerSecondProvider(provider);
        }

        public void AddAirFilter(Vector3Int cell, ICleanRoomAirFilter filter)
        {
            _registries.AddAirFilter(cell, filter);
        }

        public void RemoveAirFilter(Vector3Int cell)
        {
            _registries.RemoveAirFilter(cell);
        }

        public List<CleanRoomSaveData> GetSaveData()
        {
            return CleanRoomSavePersistence.GetSaveData(_world);
        }

        public void Restore(IReadOnlyList<CleanRoomSaveData> saveData)
        {
            CleanRoomSavePersistence.Restore(_world, saveData);
        }

        public void Destroy()
        {
            foreach (var s in _subscriptions) s.Dispose();
            _subscriptions.Clear();
        }
    }
}
