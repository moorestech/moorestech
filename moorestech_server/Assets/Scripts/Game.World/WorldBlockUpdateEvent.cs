using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.World.Interface.DataStore;
using UniRx;
using UnityEngine;

namespace Game.World
{
    public class WorldBlockUpdateEvent : IWorldBlockUpdateEvent
    {
        private readonly Subject<BlockPlaceProperties> _onBlockPlaceEvent = new();
        private readonly Subject<BlockRemoveProperties> _onBlockRemoveEvent = new();
        private readonly Dictionary<Vector3Int, List<Action<BlockPlaceProperties>>> _placeSubscribersByPos = new();
        private readonly Dictionary<Vector3Int, List<Action<BlockRemoveProperties>>> _removeSubscribersByPos = new();
        public IObservable<BlockPlaceProperties> OnBlockPlaceEvent => _onBlockPlaceEvent;
        
        public IObservable<BlockRemoveProperties> OnBlockRemoveEvent => _onBlockRemoveEvent;
        
        public IDisposable SubscribePlace(Vector3Int subscribePos, Action<BlockPlaceProperties> blockPlaceEvent)
        {
            return AddSubscriber(_placeSubscribersByPos, subscribePos, blockPlaceEvent);
        }
        
        public IDisposable SubscribeRemove(Vector3Int subscribePos, Action<BlockRemoveProperties> blockRemoveEvent)
        {
            return AddSubscriber(_removeSubscribersByPos, subscribePos, blockRemoveEvent);
        }
        
        public void OnBlockPlaceEventInvoke(Vector3Int pos, WorldBlockData worldBlockData)
        {
            // 従来の全体イベント購読者へ設置を通知する
            // Notify existing global subscribers about the placement
            _onBlockPlaceEvent.OnNext(new BlockPlaceProperties(pos, worldBlockData));

            // 座標購読者は占有セルごとの辞書で直接引く
            // Dispatch coordinate subscribers by occupied-cell dictionary lookup
            foreach (var occupiedPos in worldBlockData.BlockPositionInfo.EnumeratePositions())
                PublishToPositionSubscribers(_placeSubscribersByPos, occupiedPos, new BlockPlaceProperties(occupiedPos, worldBlockData));
        }
        
        public void OnBlockRemoveEventInvoke(Vector3Int pos, WorldBlockData worldBlockData, BlockRemoveReason removeReason)
        {
            // 従来の全体イベント購読者へ削除を通知する
            // Notify existing global subscribers about the removal
            _onBlockRemoveEvent.OnNext(new BlockRemoveProperties(pos, worldBlockData, removeReason));

            // 座標購読者は占有セルごとの辞書で直接引く
            // Dispatch coordinate subscribers by occupied-cell dictionary lookup
            foreach (var occupiedPos in worldBlockData.BlockPositionInfo.EnumeratePositions())
                PublishToPositionSubscribers(_removeSubscribersByPos, occupiedPos, new BlockRemoveProperties(occupiedPos, worldBlockData, removeReason));
        }

        private IDisposable AddSubscriber<TProperties>(
            Dictionary<Vector3Int, List<Action<TProperties>>> subscribersByPos,
            Vector3Int subscribePos,
            Action<TProperties> blockEvent)
        {
            if (!subscribersByPos.TryGetValue(subscribePos, out var subscribers))
            {
                subscribers = new List<Action<TProperties>>();
                subscribersByPos.Add(subscribePos, subscribers);
            }

            subscribers.Add(blockEvent);
            var disposed = false;
            return Disposable.Create(() =>
            {
                if (disposed) return;
                disposed = true;

                // 最後の購読者が消えた座標は辞書から外す
                // Remove the coordinate entry after its last subscriber is disposed
                subscribers.Remove(blockEvent);
                if (subscribers.Count == 0) subscribersByPos.Remove(subscribePos);
            });
        }

        private void PublishToPositionSubscribers<TProperties>(
            Dictionary<Vector3Int, List<Action<TProperties>>> subscribersByPos,
            Vector3Int position,
            TProperties properties)
        {
            if (!subscribersByPos.TryGetValue(position, out var subscribers)) return;

            // 通知中の購読解除でList列挙が壊れないようにする
            // Snapshot subscribers so disposal during notification cannot break iteration
            foreach (var subscriber in subscribers.ToArray())
                subscriber(properties);
        }
    }
}
