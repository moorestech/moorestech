using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.World.DataStore;
using Game.World.Interface.DataStore;
using UniRx;
using UnityEngine;

namespace Game.World
{
    public class WorldBlockUpdateEvent : IWorldBlockUpdateEvent
    {
        private readonly Subject<BlockPlaceProperties> _onBlockPlaceEvent = new();
        private readonly Subject<BlockRemoveProperties> _onBlockRemoveEvent = new();
        private readonly Dictionary<Vector3Int, List<Action<BlockPlaceProperties>>> _placeSubscriptions = new();
        private readonly Dictionary<Vector3Int, List<Action<BlockRemoveProperties>>> _removeSubscriptions = new();
        public IObservable<BlockPlaceProperties> OnBlockPlaceEvent => _onBlockPlaceEvent;
        public IObservable<BlockRemoveProperties> OnBlockRemoveEvent => _onBlockRemoveEvent;

        public IDisposable SubscribePlace(Vector3Int subscribePos, Action<BlockPlaceProperties> blockPlaceEvent)
        {
            return AddSubscription(_placeSubscriptions, subscribePos, blockPlaceEvent);
        }

        public IDisposable SubscribeRemove(Vector3Int subscribePos, Action<BlockRemoveProperties> blockRemoveEvent)
        {
            return AddSubscription(_removeSubscriptions, subscribePos, blockRemoveEvent);
        }

        public void OnBlockPlaceEventInvoke(Vector3Int pos, WorldBlockData worldBlockData)
        {
            PublishBlockPlace(pos, worldBlockData, false);
        }

        public void OnInitialBlockLoadPlaceEventInvoke(Vector3Int pos, WorldBlockData worldBlockData)
        {
            PublishBlockPlace(pos, worldBlockData, true);
        }

        public void OnBlockRemoveEventInvoke(Vector3Int pos, WorldBlockData worldBlockData, BlockRemoveReason removeReason)
        {
            var properties = new BlockRemoveProperties(pos, worldBlockData, removeReason);
            _onBlockRemoveEvent.OnNext(properties);
            NotifyRemoveSubscriptions(worldBlockData, removeReason);
        }

        private void PublishBlockPlace(Vector3Int pos, WorldBlockData worldBlockData, bool isInitialLoad)
        {
            // 全体イベントは電力などの内部系へ流し、座標指定購読は辞書で直接通知する
            // Send global events for systems like power, and dispatch coordinate subscribers directly.
            var properties = new BlockPlaceProperties(pos, worldBlockData, isInitialLoad);
            _onBlockPlaceEvent.OnNext(properties);
            NotifyPlaceSubscriptions(worldBlockData, isInitialLoad);
        }

        private void NotifyPlaceSubscriptions(WorldBlockData worldBlockData, bool isInitialLoad)
        {
            // 占有座標ごとの購読だけを呼び、巨大な全購読者走査を避ける
            // Invoke only matching occupied-coordinate subscribers to avoid scanning every subscriber.
            foreach (var pos in BlockCoordinateIndex.EnumeratePositions(worldBlockData.BlockPositionInfo))
            {
                if (!_placeSubscriptions.TryGetValue(pos, out var actions)) continue;
                var properties = new BlockPlaceProperties(pos, worldBlockData, isInitialLoad);
                InvokeActions(actions, properties);
            }
        }

        private void NotifyRemoveSubscriptions(WorldBlockData worldBlockData, BlockRemoveReason removeReason)
        {
            // 削除も配置と同じ座標範囲で通知し、複数マスブロックの互換性を保つ
            // Dispatch removal over the same footprint to preserve multi-cell block behavior.
            foreach (var pos in BlockCoordinateIndex.EnumeratePositions(worldBlockData.BlockPositionInfo))
            {
                if (!_removeSubscriptions.TryGetValue(pos, out var actions)) continue;
                var properties = new BlockRemoveProperties(pos, worldBlockData, removeReason);
                InvokeActions(actions, properties);
            }
        }

        private static IDisposable AddSubscription<TProperties>(
            Dictionary<Vector3Int, List<Action<TProperties>>> subscriptions,
            Vector3Int pos,
            Action<TProperties> action)
        {
            if (!subscriptions.TryGetValue(pos, out var actions))
            {
                actions = new List<Action<TProperties>>();
                subscriptions.Add(pos, actions);
            }
            actions.Add(action);
            return new Subscription(() => RemoveSubscription(subscriptions, pos, actions, action));
        }

        private static void RemoveSubscription<TProperties>(
            Dictionary<Vector3Int, List<Action<TProperties>>> subscriptions,
            Vector3Int pos,
            List<Action<TProperties>> actions,
            Action<TProperties> action)
        {
            actions.Remove(action);
            if (actions.Count == 0) subscriptions.Remove(pos);
        }

        private static void InvokeActions<TProperties>(List<Action<TProperties>> actions, TProperties properties)
        {
            var dispatchActions = actions.ToArray();
            for (var i = 0; i < dispatchActions.Length; i++) dispatchActions[i](properties);
        }

        private class Subscription : IDisposable
        {
            private Action _dispose;

            public Subscription(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                if (_dispose == null) return;
                _dispose();
                _dispose = null;
            }
        }
    }
}
