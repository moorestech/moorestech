using System;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.World.Interface.DataStore;
using UniRx;
using UnityEngine;

namespace Game.World
{
    public class WorldBlockUpdateEvent : IWorldBlockUpdateEvent
    {
        private readonly Subject<BlockUpdateProperties> _onBlockPlaceEvent = new();
        private readonly Subject<BlockUpdateProperties> _onBlockRemoveEvent = new();
        public IObservable<BlockUpdateProperties> OnBlockPlaceEvent => _onBlockPlaceEvent;
        
        public IObservable<BlockUpdateProperties> OnBlockRemoveEvent => _onBlockRemoveEvent;
        
        public IDisposable SubscribePlace(Vector3Int subscribePos, Action<BlockUpdateProperties> blockPlaceEvent)
        {
            return _onBlockPlaceEvent.Subscribe(data =>
            {
                if (data.BlockData.BlockPositionInfo.IsContainPos(subscribePos)) blockPlaceEvent(new BlockUpdateProperties(subscribePos, data.BlockData));
            });
        }
        
        public IDisposable SubscribeRemove(Vector3Int subscribePos, Action<BlockUpdateProperties> blockPlaceEvent)
        {
            return _onBlockRemoveEvent.Subscribe(data =>
            {
                if (data.BlockData.BlockPositionInfo.IsContainPos(subscribePos)) blockPlaceEvent(new BlockUpdateProperties(subscribePos, data.BlockData));
            });
        }
        
        public void OnBlockPlaceEventInvoke(Vector3Int pos, WorldBlockData worldBlockData)
        {
            _onBlockPlaceEvent.OnNext(new BlockUpdateProperties(pos, worldBlockData));
        }
        
        public void OnBlockRemoveEventInvoke(Vector3Int pos, WorldBlockData worldBlockData)
        {
            OnBlockRemoveEventInvoke(pos, worldBlockData, BlockRemoveReason.ManualRemove);
        }
        
        public void OnBlockRemoveEventInvoke(Vector3Int pos, WorldBlockData worldBlockData, BlockRemoveReason removeReason)
        {
            _onBlockRemoveEvent.OnNext(new BlockUpdateProperties(pos, worldBlockData, removeReason));
        }
    }
}
