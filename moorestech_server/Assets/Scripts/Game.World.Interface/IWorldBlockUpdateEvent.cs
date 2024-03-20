using System;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.World.Interface
{
    public interface IWorldBlockUpdateEvent
    {
        public IObservable<WorldBlockData> OnBlockPlaceEvent { get; }
        public IObservable<WorldBlockData> OnBlockRemoveEvent { get; }

        /// <summary>
        /// 特定の座標にブロックが置かれた時のイベントを購読する
        /// </summary>
        public IDisposable SubscribePlace(Vector3Int subscribePos, Action<WorldBlockData> blockPlaceEvent);
        
        /// <summary>
        /// 特定の座標にブロックが削除された時のイベントを購読する
        /// </summary>
        public IDisposable SubscribeRemove(Vector3Int subscribePos, Action<WorldBlockData> blockPlaceEvent);
    }
}