using MainGame.UnityView.Interface;
using UnityEngine;
using static MainGame.UnityView.Interface.IBlockUpdateEvent;

namespace MainGame.GameLogic.Chunk
{
    public class BlockUpdateEvent : IBlockUpdateEvent
    {
        private event OnBlockPlaceEvent OnBlockPlace;
        private event OnBlockRemoveEvent OnBlockRemove;
        
        
        public void Subscribe(OnBlockPlaceEvent onBlockPlaceEvent, OnBlockRemoveEvent onBlockRemoveEvent)
        {
            OnBlockPlace += onBlockPlaceEvent;
            OnBlockRemove += onBlockRemoveEvent;
        }

        public void UnSubscribe(OnBlockPlaceEvent onBlockPlaceEvent, OnBlockRemoveEvent onBlockRemoveEvent)
        {
            OnBlockPlace -= onBlockPlaceEvent;
            OnBlockRemove -= onBlockRemoveEvent;
        }

        internal void OnOnBlockPlace(Vector2Int blockposition, int blockid)
        {
            OnBlockPlace?.Invoke(blockposition, blockid);
        }

        internal void OnOnBlockRemove(Vector2Int blockposition)
        {
            OnBlockRemove?.Invoke(blockposition);
        }
    }
}