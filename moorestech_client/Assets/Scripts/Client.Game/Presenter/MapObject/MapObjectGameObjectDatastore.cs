using System.Collections.Generic;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using MainGame.UnityView.MapObject;
using Server.Event.EventReceive;
using UnityEngine;
using VContainer;

namespace MainGame.Presenter.MapObject
{
    /// <summary>
    ///     TODO 静的なオブジェクトになってるので、サーバーからコンフィグを取得して動的に生成するようにしたい
    /// </summary>
    public class MapObjectGameObjectDatastore : MonoBehaviour
    {
        [SerializeField] private List<MapObjectGameObject> mapObjects;
        private readonly Dictionary<int, MapObjectGameObject> _allMapObjects = new();

#if UNITY_EDITOR
        public IReadOnlyList<MapObjectGameObject> MapObjects => mapObjects;
#endif
        
        [Inject]
        public void Construct()
        {
            VanillaApi.Event.RegisterEventResponse(MapObjectUpdateEventPacket.EventTag,OnUpdateMapObject);
        }

        private async UniTask Awake()
        {
            foreach (var mapObject in mapObjects) _allMapObjects.Add(mapObject.InstanceId, mapObject);
            
            var mapObjectInfos = await VanillaApi.Response.GetMapObjectInfo(default);
            
            foreach (var mapObjectInfo in mapObjectInfos)
            {
                var mapObject = _allMapObjects[mapObjectInfo.InstanceId];
                if (mapObjectInfo.IsDestroyed)
                {
                    mapObject.DestroyMapObject();
                }
            }
        }

        private void OnUpdateMapObject(byte[] payLoad)
        {
            var data = MessagePack.MessagePackSerializer.Deserialize<MapObjectUpdateEventMessagePack>(payLoad);
            
            switch (data.EventType)
            {
                case MapObjectUpdateEventMessagePack.DestroyEventType:
                    _allMapObjects[data.InstanceId].DestroyMapObject();
                    break;
                default:
                    throw new System.Exception("MapObjectUpdateEventProtocol: EventTypeが不正か実装されていません");
            }
        }
    }
}