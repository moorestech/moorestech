using System;
using Game.World.Interface;
using Game.World.Interface.DataStore;
using UniRx;
using UnityEngine;

namespace Game.World
{
    public class WorldBlockUpdateEvent : IWorldBlockUpdateEvent
    {
        public IObservable<WorldBlockData> OnBlockPlaceEvent => _onBlockPlaceEvent;
        private readonly Subject<WorldBlockData> _onBlockPlaceEvent = new();
        
        public IObservable<WorldBlockData> OnBlockRemoveEvent => _onBlockRemoveEvent;
        private readonly Subject<WorldBlockData> _onBlockRemoveEvent = new();
        
        public IDisposable SubscribePlace(Vector3Int subscribePos, Action<BlockUpdateProperties> blockPlaceEvent)
        { 
            return _onBlockPlaceEvent.Subscribe(data =>
            {
                if (data.BlockPositionInfo.IsContainPos(subscribePos))
                {
                    blockPlaceEvent(new BlockUpdateProperties(subscribePos, data));
                }
            });
        }

        public IDisposable SubscribeRemove(Vector3Int subscribePos, Action<BlockUpdateProperties> blockPlaceEvent)
        {
            return _onBlockRemoveEvent.Subscribe(data =>
            {
                if (data.BlockPositionInfo.IsContainPos(subscribePos))
                {
                    blockPlaceEvent(new BlockUpdateProperties(subscribePos, data));
                }
            });
        }
        
        public void OnBlockPlaceEventInvoke(WorldBlockData worldBlockData)
        {
            _onBlockPlaceEvent.OnNext(worldBlockData);
        }
        
        public void OnBlockRemoveEventInvoke(WorldBlockData worldBlockData)
        {
            _onBlockRemoveEvent.OnNext(worldBlockData);
        }
    }
}