using System;
using UniRx;
using UnityEngine;

namespace Game.World.Interface.DataStore
{
    public interface IWorldBlockUpdateEvent
    {
        public IObservable<BlockPlaceProperties> OnBlockPlaceEvent { get; }
        public IObservable<BlockRemoveProperties> OnBlockRemoveEvent { get; }

        // tick末尾でブロック状態の変化が出揃った合図
        // Signals at tick end that this tick's block state changes are all in
        public IObservable<Unit> OnTickEndStatesCommitted { get; }
        
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
