using System;
using Game.MapObject.Interface;
using UnityEngine;

namespace Game.MapObject
{
    /// <summary>
    ///     木や小石など基本的に動かないマップオブジェクト
    /// </summary>
    public class VanillaStaticMapObject : IMapObject
    {
        public VanillaStaticMapObject(int id, string type, bool isDestroyed, Vector3 position, int itemId,
            int itemCount)
        {
            InstanceId = id;
            Type = type;
            IsDestroyed = isDestroyed;
            Position = position;
            ItemId = itemId;
            ItemCount = itemCount;
        }

        public int InstanceId { get; }
        public string Type { get; }
        public bool IsDestroyed { get; private set; }
        public Vector3 Position { get; }
        public int ItemId { get; }
        public int ItemCount { get; }

        public event Action OnDestroy;

        public void Destroy()
        {
            IsDestroyed = true;
            OnDestroy?.Invoke();
        }
    }
}