using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace MainGame.Network.Event
{
    public class ReceiveUpdateMapObjectEvent
    {
        public event Action<MapObjectProperties> OnDestroyMapObject;
        public event Action<List<MapObjectProperties>> OnReceiveMapObjectInformation;


        internal async UniTask InvokeOnDestroyMapObject(MapObjectProperties properties)
        {
            await UniTask.SwitchToMainThread();
            OnDestroyMapObject?.Invoke(properties);
        }
        
        
        internal async UniTask InvokeReceiveMapObjectInformation(List<MapObjectProperties> properties)
        {
            await UniTask.SwitchToMainThread();
            OnReceiveMapObjectInformation?.Invoke(properties);
        }
            
    }

    public class MapObjectProperties
    {
        public int InstanceId { get; }
        public bool IsDestroyed { get; }
        public MapObjectProperties(int instanceId, bool isDestroyed)
        {
            InstanceId = instanceId;
            IsDestroyed = isDestroyed;
        }

    }
}