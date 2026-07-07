using System;
using System.Collections.Generic;
using Core.Update;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using UniRx;
using UnityEngine;

namespace Game.CleanRoom
{
    /// <summary>
    ///     ブロック増減を購読し、tick毎の差分再検出で部屋一覧を保持する
    ///     Subscribes to block changes and maintains rooms via per-tick incremental detection
    /// </summary>
    public class CleanRoomDatastore
    {
        public const int DirtyCellBudgetPerTick = 8192;

        public IReadOnlyList<CleanRoom> Rooms => _detectionService.Rooms;

        private readonly CleanRoomDetectionService _detectionService;
        private readonly List<IDisposable> _subscriptions = new();

        public CleanRoomDatastore(IWorldBlockDatastore worldBlockDatastore)
        {
            _detectionService = new CleanRoomDetectionService(worldBlockDatastore, DirtyCellBudgetPerTick);

            // tick毎のdirty消化と、設置・破壊イベントによるシード投入を購読する
            // Subscribe per-tick dirty processing plus place/remove events for seeding
            _subscriptions.Add(GameUpdater.UpdateObservable.Subscribe(_ => _detectionService.ProcessDirtySeeds()));
            _subscriptions.Add(ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(properties => _detectionService.OnBlockChanged(properties.BlockData)));
            _subscriptions.Add(ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(properties => _detectionService.OnBlockChanged(properties.BlockData)));
        }

        public bool TryGetCleanRoomAt(Vector3Int cell, out CleanRoom room)
        {
            foreach (var candidate in _detectionService.Rooms)
            {
                if (!candidate.Contains(cell)) continue;
                room = candidate;
                return true;
            }

            room = null;
            return false;
        }

        public bool TryGetCleanRoom(IBlock block, out CleanRoom room)
        {
            room = null;
            var minPos = block.BlockPositionInfo.MinPos;
            var maxPos = block.BlockPositionInfo.MaxPos;

            // 全占有セルが同一部屋に属するときのみ成功とする
            // Succeed only when every occupied cell belongs to the same room
            for (var x = minPos.x; x <= maxPos.x; x++)
            for (var y = minPos.y; y <= maxPos.y; y++)
            for (var z = minPos.z; z <= maxPos.z; z++)
            {
                if (!TryGetCleanRoomAt(new Vector3Int(x, y, z), out var cellRoom) || (room != null && room != cellRoom))
                {
                    room = null;
                    return false;
                }

                room = cellRoom;
            }

            return room != null;
        }

        public void RebuildAll()
        {
            _detectionService.RebuildAll();
        }

        public void Destroy()
        {
            foreach (var subscription in _subscriptions) subscription.Dispose();
            _subscriptions.Clear();
        }
    }
}
