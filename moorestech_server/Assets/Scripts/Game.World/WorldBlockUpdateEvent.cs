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
        private readonly Dictionary<Vector3Int, CoordinateEventSubject<BlockPlaceProperties>> _placeSubjectsByPos = new();
        private readonly Dictionary<Vector3Int, CoordinateEventSubject<BlockRemoveProperties>> _removeSubjectsByPos = new();
        public IObservable<BlockPlaceProperties> OnBlockPlaceEvent => _onBlockPlaceEvent;
        
        public IObservable<BlockRemoveProperties> OnBlockRemoveEvent => _onBlockRemoveEvent;
        
        public IObservable<BlockPlaceProperties> GetBlockPlaceEvent(Vector3Int subscribePos)
        {
            return Observable.Create<BlockPlaceProperties>(observer => SubscribeCoordinateEvent(_placeSubjectsByPos, subscribePos, observer));
        }
        
        public IObservable<BlockRemoveProperties> GetBlockRemoveEvent(Vector3Int subscribePos)
        {
            return Observable.Create<BlockRemoveProperties>(observer => SubscribeCoordinateEvent(_removeSubjectsByPos, subscribePos, observer));
        }
        
        public void OnBlockPlaceEventInvoke(Vector3Int pos, WorldBlockData worldBlockData)
        {
            // 全体購読者へブロック設置を通知する
            // Notify global subscribers about the block placement
            _onBlockPlaceEvent.OnNext(new BlockPlaceProperties(pos, worldBlockData));

            // 占有セルごとのSubjectへ座標指定イベントを配送する
            // Dispatch coordinate events to subjects keyed by occupied cell
            foreach (var occupiedPos in worldBlockData.BlockPositionInfo.EnumeratePositions())
                PublishCoordinateEvent(_placeSubjectsByPos, occupiedPos, new BlockPlaceProperties(occupiedPos, worldBlockData));
        }
        
        public void OnBlockRemoveEventInvoke(Vector3Int pos, WorldBlockData worldBlockData, BlockRemoveReason removeReason)
        {
            // 全体購読者へブロック削除を通知する
            // Notify global subscribers about the block removal
            _onBlockRemoveEvent.OnNext(new BlockRemoveProperties(pos, worldBlockData, removeReason));

            // 占有セルごとのSubjectへ座標指定イベントを配送する
            // Dispatch coordinate events to subjects keyed by occupied cell
            foreach (var occupiedPos in worldBlockData.BlockPositionInfo.EnumeratePositions())
                PublishCoordinateEvent(_removeSubjectsByPos, occupiedPos, new BlockRemoveProperties(occupiedPos, worldBlockData, removeReason));
        }

        private IDisposable SubscribeCoordinateEvent<TProperties>(
            Dictionary<Vector3Int, CoordinateEventSubject<TProperties>> subjectsByPos,
            Vector3Int subscribePos,
            IObserver<TProperties> observer)
        {
            if (!subjectsByPos.TryGetValue(subscribePos, out var subject))
            {
                subject = new CoordinateEventSubject<TProperties>();
                subjectsByPos.Add(subscribePos, subject);
            }

            // 最後の購読が破棄された座標は辞書から削除する
            // Remove the coordinate entry after its last subscription is disposed
            var subscription = subject.Subscribe(observer);
            var disposed = false;
            return Disposable.Create(() =>
            {
                if (disposed) return;
                disposed = true;
                subscription.Dispose();
                if (!subject.HasSubscriber() &&
                    subjectsByPos.TryGetValue(subscribePos, out var currentSubject) &&
                    ReferenceEquals(currentSubject, subject))
                    subjectsByPos.Remove(subscribePos);
            });
        }

        private void PublishCoordinateEvent<TProperties>(
            Dictionary<Vector3Int, CoordinateEventSubject<TProperties>> subjectsByPos,
            Vector3Int position,
            TProperties properties)
        {
            if (!subjectsByPos.TryGetValue(position, out var subject)) return;
            subject.OnNext(properties);
        }

        private sealed class CoordinateEventSubject<TProperties>
        {
            private readonly Subject<TProperties> _subject = new();
            private int _subscriberCount;

            public bool HasSubscriber()
            {
                return _subscriberCount > 0;
            }

            public IDisposable Subscribe(IObserver<TProperties> observer)
            {
                _subscriberCount++;
                var subscription = _subject.Subscribe(observer);
                var disposed = false;
                return Disposable.Create(() =>
                {
                    if (disposed) return;
                    disposed = true;
                    subscription.Dispose();
                    _subscriberCount--;
                });
            }

            public void OnNext(TProperties properties)
            {
                _subject.OnNext(properties);
            }
        }
    }
}
