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
        private readonly Dictionary<Vector3Int, Subject<BlockPlaceProperties>> _placeSubjectsByPos = new();
        private readonly Dictionary<Vector3Int, Subject<BlockRemoveProperties>> _removeSubjectsByPos = new();
        public IObservable<BlockPlaceProperties> OnBlockPlaceEvent => _onBlockPlaceEvent;
        
        public IObservable<BlockRemoveProperties> OnBlockRemoveEvent => _onBlockRemoveEvent;
        
        public IObservable<BlockPlaceProperties> GetBlockPlaceEvent(Vector3Int subscribePos)
        {
            return Observable.Create<BlockPlaceProperties>(
                observer => SubscribeCoordinateEvent(_placeSubjectsByPos, subscribePos, observer),
                false);
        }
        
        public IObservable<BlockRemoveProperties> GetBlockRemoveEvent(Vector3Int subscribePos)
        {
            return Observable.Create<BlockRemoveProperties>(
                observer => SubscribeCoordinateEvent(_removeSubjectsByPos, subscribePos, observer),
                false);
        }
        
        public void OnBlockPlaceEventInvoke(Vector3Int pos, WorldBlockData worldBlockData, bool isInitialLoad)
        {
            // 全体購読者へブロック設置を通知する
            // Notify global subscribers about the block placement
            _onBlockPlaceEvent.OnNext(new BlockPlaceProperties(pos, worldBlockData, isInitialLoad));

            // 占有セルごとのSubjectへ座標指定イベントを配送する
            // Dispatch coordinate events to subjects keyed by occupied cell
            foreach (var occupiedPos in worldBlockData.BlockPositionInfo.EnumeratePositions())
                PublishPlaceCoordinateEvent(occupiedPos, worldBlockData, isInitialLoad);
        }
        
        public void OnBlockRemoveEventInvoke(Vector3Int pos, WorldBlockData worldBlockData, BlockRemoveReason removeReason)
        {
            // 全体購読者へブロック削除を通知する
            // Notify global subscribers about the block removal
            _onBlockRemoveEvent.OnNext(new BlockRemoveProperties(pos, worldBlockData, removeReason));

            // 占有セルごとのSubjectへ座標指定イベントを配送する
            // Dispatch coordinate events to subjects keyed by occupied cell
            foreach (var occupiedPos in worldBlockData.BlockPositionInfo.EnumeratePositions())
                PublishRemoveCoordinateEvent(occupiedPos, worldBlockData, removeReason);
        }

        private IDisposable SubscribeCoordinateEvent<TProperties>(
            Dictionary<Vector3Int, Subject<TProperties>> subjectsByPos,
            Vector3Int subscribePos,
            IObserver<TProperties> observer)
        {
            if (!subjectsByPos.TryGetValue(subscribePos, out var subject))
            {
                subject = new Subject<TProperties>();
                subjectsByPos.Add(subscribePos, subject);
            }

            // 最後の購読が破棄された座標は辞書から削除する
            // Remove the coordinate entry after its last subscription is disposed
            var subscription = subject.Subscribe(observer);
            return Disposable.Create(() =>
            {
                subscription.Dispose();
                if (!subject.HasObservers &&
                    subjectsByPos.TryGetValue(subscribePos, out var currentSubject) &&
                    ReferenceEquals(currentSubject, subject))
                    subjectsByPos.Remove(subscribePos);
            });
        }

        private void PublishPlaceCoordinateEvent(Vector3Int position, WorldBlockData worldBlockData, bool isInitialLoad)
        {
            if (!_placeSubjectsByPos.TryGetValue(position, out var subject)) return;
            subject.OnNext(new BlockPlaceProperties(position, worldBlockData, isInitialLoad));
        }

        private void PublishRemoveCoordinateEvent(Vector3Int position, WorldBlockData worldBlockData, BlockRemoveReason removeReason)
        {
            if (!_removeSubjectsByPos.TryGetValue(position, out var subject)) return;
            subject.OnNext(new BlockRemoveProperties(position, worldBlockData, removeReason));
        }
    }
}
