using System;
using UnityEngine;

namespace Game.World.Interface.DataStore
{
    public interface IWorldBlockUpdateEvent
    {
        public IObservable<BlockPlaceProperties> OnBlockPlaceEvent { get; }
        public IObservable<BlockRemoveProperties> OnBlockRemoveEvent { get; }
        
        /// <summary>
        ///     特定の座標にブロックが置かれた時のイベントを購読する
        /// </summary>
        public IDisposable SubscribePlace(Vector3Int subscribePos, Action<BlockPlaceProperties> blockPlaceEvent);
        
        /// <summary>
        ///     特定の座標にブロックが削除された時のイベントを購読する
        /// </summary>
        public IDisposable SubscribeRemove(Vector3Int subscribePos, Action<BlockRemoveProperties> blockRemoveEvent);
    }
}
