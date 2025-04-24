using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Network.API;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     TODO 静的なオブジェクトになってるので、サーバーからコンフィグを取得して動的に生成するようにしたい
    /// </summary>
    public class MapObjectGameObjectDatastore : MonoBehaviour
    {
        [SerializeField] private List<MapObjectGameObject> mapObjects;
        private readonly Dictionary<int, MapObjectGameObject> _allMapObjects = new();
        
        
        [Inject]
        public void Construct(InitialHandshakeResponse handshakeResponse)
        {
            //イベント登録
            ClientContext.VanillaApi.Event.SubscribeEventResponse(MapObjectUpdateEventPacket.EventTag, OnUpdateMapObject);
            
            // mapObjectの破壊状況の初期設定
            foreach (var mapObject in mapObjects) _allMapObjects.Add(mapObject.InstanceId, mapObject);
            
            foreach (var mapObjectInfo in handshakeResponse.MapObjects)
            {
                var mapObject = _allMapObjects[mapObjectInfo.InstanceId];
                if (mapObjectInfo.IsDestroyed) mapObject.DestroyMapObject();
            }
        }
        
        private void OnUpdateMapObject(byte[] payLoad)
        {
            var data = MessagePackSerializer.Deserialize<MapObjectUpdateEventMessagePack>(payLoad);
            
            switch (data.EventType)
            {
                case MapObjectUpdateEventMessagePack.DestroyEventType:
                    _allMapObjects[data.InstanceId].DestroyMapObject();
                    break;
                default:
                    throw new Exception("MapObjectUpdateEventProtocol: EventTypeが不正か実装されていません");
            }
        }
        
        public MapObjectGameObject SearchNearestMapObject(Guid mapObjectGuid, Vector3 position)
        {
            MapObjectGameObject nearestMapObject = null; 
            var distance = float.MaxValue;
            
            for (var i = 0; i < mapObjects.Count; i++)
            {
                var mapObject = mapObjects[i];
                
                // 指定されているmapObjectか破壊されていないかチェック
                if (mapObject.MapObjectGuid != mapObjectGuid || mapObject.IsDestroyed) continue;
                
                // 距離をチェック
                var currentDistance = Vector3.Distance(position, mapObject.GetPosition());
                if (distance > currentDistance) continue;
                
                nearestMapObject = mapObject;
                distance = currentDistance;
            }
            
            return nearestMapObject;
        }
        
#if UNITY_EDITOR
        public List<MapObjectGameObject> MapObjects => mapObjects;
        
        public void FindMapObjects()
        {
            mapObjects = FindObjectsOfType<MapObjectGameObject>().ToList();
            mapObjects.Sort((a, b) => string.Compare(a.gameObject.name, b.gameObject.name, StringComparison.Ordinal));
        }
#endif
    }
}