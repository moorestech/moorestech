using System.Collections.Generic;
using Client.Game.Context;
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
        public void Construct(InitialHandshakeResponse handshakeResponse)
        {
            //イベント登録
            MoorestechContext.VanillaApi.Event.RegisterEventResponse(MapObjectUpdateEventPacket.EventTag,OnUpdateMapObject);
            
            // mapObjectの破壊状況の初期設定
            foreach (var mapObject in mapObjects) _allMapObjects.Add(mapObject.InstanceId, mapObject);
            
            foreach (var mapObjectInfo in handshakeResponse.MapObjects)
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