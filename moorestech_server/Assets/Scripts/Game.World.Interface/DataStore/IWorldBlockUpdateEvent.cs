using System;
using UnityEngine;

namespace Game.World.Interface.DataStore
{
    public interface IWorldBlockUpdateEvent
    {
        public IObservable<BlockUpdateProperties> OnBlockPlaceEvent { get; }
        public IObservable<BlockUpdateProperties> OnBlockRemoveEvent { get; }
        
        /// <summary>
        ///     特定の座標にブロックが置かれた時のイベントを購読する
        /// </summary>
        public IDisposable SubscribePlace(Vector3Int subscribePos, Action<BlockUpdateProperties> blockPlaceEvent);
        
        /// <summary>
        ///     特定の座標にブロックが削除された時のイベントを購読する
        /// </summary>
        public IDisposable SubscribeRemove(Vector3Int subscribePos, Action<BlockUpdateProperties> blockPlaceEvent);
    }
    
    public class BlockUpdateProperties
    {
        public Vector3Int Pos { get; }
        public WorldBlockData BlockData { get; }
        
        public BlockUpdateProperties(Vector3Int pos, WorldBlockData blockData)
        {
            Pos = pos;
            BlockData = blockData;
        }
    }
}