using System.Collections.Generic;
using MainGame.Network.Event;
using MainGame.UnityView.MapObject;
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

        private void Awake()
        {
            foreach (var mapObject in mapObjects) _allMapObjects.Add(mapObject.InstanceId, mapObject);
        }


        [Inject]
        public void Construct(ReceiveUpdateMapObjectEvent receiveUpdateMapObjectEvent)
        {
            receiveUpdateMapObjectEvent.OnReceiveMapObjectInformation += UpdateMapObjectInformation;
            receiveUpdateMapObjectEvent.OnDestroyMapObject += DestroyMapObject;
        }

        private void DestroyMapObject(MapObjectProperties mapObject)
        {
            _allMapObjects[mapObject.InstanceId].DestroyMapObject();
        }


        private void UpdateMapObjectInformation(List<MapObjectProperties> mapObjects)
        {
            foreach (var mapObject in mapObjects)
                if (mapObject.IsDestroyed)
                    _allMapObjects[mapObject.InstanceId].DestroyMapObject();
        }
    }
}