using System;
using Game.Base;
using Game.MapObject.Interface;

namespace Game.MapObject
{
    /// <summary>
    /// 木や小石など基本的に動かないマップオブジェクト
    /// </summary>
    public class VanillaStaticMapObject : IMapObject
    {
        public int InstanceId { get; }
        public string Type { get; }
        public bool IsDestroyed { get; private set; }
        public ServerVector3 Position { get; }
        public int ItemId { get; }
        public int ItemCount { get; }

        public event Action OnDestroy;
        
        public VanillaStaticMapObject(int id, string type, bool isDestroyed, ServerVector3 position, int itemId,int itemCount)
        {
            InstanceId = id;
            Type = type;
            IsDestroyed = isDestroyed;
            Position = position;
            ItemId = itemId;
            ItemCount = itemCount;
        }

        public void Destroy()
        {
            IsDestroyed = true;
            OnDestroy?.Invoke();
        }

    }
}