using System;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.World.Interface.DataStore;
using UniRx;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Game.World
{
    public class WorldBlockUpdateEvent : IWorldBlockUpdateEvent
    {
        private readonly Subject<BlockPlaceProperties> _onBlockPlaceEvent = new();
        private readonly Subject<BlockRemoveProperties> _onBlockRemoveEvent = new();
        private double _placeEventElapsedMs;
        private int _placeEventInvokeCount;
        private int _placeSubscriberCount;
        private int _removeSubscriberCount;
        public IObservable<BlockPlaceProperties> OnBlockPlaceEvent => _onBlockPlaceEvent;
        
        public IObservable<BlockRemoveProperties> OnBlockRemoveEvent => _onBlockRemoveEvent;
        
        public IDisposable SubscribePlace(Vector3Int subscribePos, Action<BlockPlaceProperties> blockPlaceEvent)
        {
            _placeSubscriberCount++;
            return _onBlockPlaceEvent.Subscribe(data =>
            {
                if (data.BlockData.BlockPositionInfo.IsContainPos(subscribePos)) blockPlaceEvent(new BlockPlaceProperties(subscribePos, data.BlockData));
            });
        }
        
        public IDisposable SubscribeRemove(Vector3Int subscribePos, Action<BlockRemoveProperties> blockRemoveEvent)
        {
            _removeSubscriberCount++;
            return _onBlockRemoveEvent.Subscribe(data =>
            {
                if (data.BlockData.BlockPositionInfo.IsContainPos(subscribePos)) blockRemoveEvent(new BlockRemoveProperties(subscribePos, data.BlockData, data.RemoveReason));
            });
        }
        
        public void OnBlockPlaceEventInvoke(Vector3Int pos, WorldBlockData worldBlockData)
        {
            var stopwatch = Stopwatch.StartNew();
            _onBlockPlaceEvent.OnNext(new BlockPlaceProperties(pos, worldBlockData));
            stopwatch.Stop();

            // 起動時の配置イベント累積時間を確認する
            // Track cumulative place-event cost during startup.
            _placeEventElapsedMs += stopwatch.Elapsed.TotalMilliseconds;
            _placeEventInvokeCount++;
            if (_placeEventInvokeCount % 1000 == 0)
            {
                Debug.Log($"[StartupProfile] WorldBlockPlaceEvent invokes={_placeEventInvokeCount} subscribers={_placeSubscriberCount} removeSubscribers={_removeSubscriberCount} elapsedMs={_placeEventElapsedMs:F3}");
            }
        }
        
        public void OnBlockRemoveEventInvoke(Vector3Int pos, WorldBlockData worldBlockData, BlockRemoveReason removeReason)
        {
            _onBlockRemoveEvent.OnNext(new BlockRemoveProperties(pos, worldBlockData, removeReason));
        }
    }
}
