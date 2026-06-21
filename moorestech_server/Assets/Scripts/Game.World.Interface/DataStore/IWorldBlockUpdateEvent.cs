using System;
using UnityEngine;

namespace Game.World.Interface.DataStore
{
    public interface IWorldBlockUpdateEvent
    {
        public IObservable<BlockPlaceProperties> OnBlockPlaceEvent { get; }
        public IObservable<BlockRemoveProperties> OnBlockRemoveEvent { get; }
        
        /// <summary>
        ///     特定の座標にブロックが置かれた時のイベントを取得する
        ///     Gets the event stream for block placement at a specific coordinate.
        /// </summary>
        public IObservable<BlockPlaceProperties> GetBlockPlaceEvent(Vector3Int subscribePos);
        
        /// <summary>
        ///     特定の座標にブロックが削除された時のイベントを取得する
        ///     Gets the event stream for block removal at a specific coordinate.
        /// </summary>
        public IObservable<BlockRemoveProperties> GetBlockRemoveEvent(Vector3Int subscribePos);
    }
}
