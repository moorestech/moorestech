using System;
using System.Collections.Generic;
using MainGame.Network.Event;
using MainGame.UnityView.MapObject;
using UnityEngine;
using VContainer;

namespace MainGame.Presenter.MapObject
{
    /// <summary>
    /// TODO 静的なオブジェクトになってるので、サーバーからコンフィグを取得して動的に生成するようにしたい
    /// </summary>
    public class MapObjectGameObjectDatastore : MonoBehaviour
    {
        [SerializeField] private List<MapObjectGameObject> stoneMapObjects;
        [SerializeField] private List<MapObjectGameObject> treeMapObjects;
        [SerializeField] private List<MapObjectGameObject> bushMapObjects;

#if UNITY_EDITOR
        public IReadOnlyList<MapObjectGameObject> StoneMapObjects => stoneMapObjects;
        public IReadOnlyList<MapObjectGameObject> TreeMapObjects => treeMapObjects;
        public IReadOnlyList<MapObjectGameObject> BushMapObjects => bushMapObjects;
#endif


        private readonly Dictionary<int, MapObjectGameObject> _allMapObjects = new();

        private void Awake()
        {
            foreach (var stone in stoneMapObjects)
            {
                _allMapObjects.Add(stone.InstanceId, stone);
            }

            foreach (var tree in treeMapObjects)
            {
                _allMapObjects.Add(tree.InstanceId, tree);
            }

            foreach (var bush in bushMapObjects)
            {
                _allMapObjects.Add(bush.InstanceId, bush);
            }
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
            {
                if (mapObject.IsDestroyed)
                {
                    _allMapObjects[mapObject.InstanceId].DestroyMapObject();
                }
            }
        } 
        
        
    }
}