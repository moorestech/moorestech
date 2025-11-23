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
        private readonly Subject<BlockPlaceProperties> _onBlockPlaceEvent = new();
        private readonly Subject<BlockRemoveProperties> _onBlockRemoveEvent = new();
        public IObservable<BlockPlaceProperties> OnBlockPlaceEvent => _onBlockPlaceEvent;
        
        public IObservable<BlockRemoveProperties> OnBlockRemoveEvent => _onBlockRemoveEvent;
        
        public IDisposable SubscribePlace(Vector3Int subscribePos, Action<BlockPlaceProperties> blockPlaceEvent)
        {
            return _onBlockPlaceEvent.Subscribe(data =>
            {
                if (data.BlockData.BlockPositionInfo.IsContainPos(subscribePos)) blockPlaceEvent(new BlockPlaceProperties(subscribePos, data.BlockData));
            });
        }
        
        public IDisposable SubscribeRemove(Vector3Int subscribePos, Action<BlockRemoveProperties> blockRemoveEvent)
        {
            return _onBlockRemoveEvent.Subscribe(data =>
            {
                if (data.BlockData.BlockPositionInfo.IsContainPos(subscribePos)) blockRemoveEvent(new BlockRemoveProperties(subscribePos, data.BlockData, data.RemoveReason));
            });
        }
        
        public void OnBlockPlaceEventInvoke(Vector3Int pos, WorldBlockData worldBlockData)
        {
            _onBlockPlaceEvent.OnNext(new BlockPlaceProperties(pos, worldBlockData));
        }
        
        public void OnBlockRemoveEventInvoke(Vector3Int pos, WorldBlockData worldBlockData, BlockRemoveReason removeReason)
        {
            _onBlockRemoveEvent.OnNext(new BlockRemoveProperties(pos, worldBlockData, removeReason));
        }
    }
}
