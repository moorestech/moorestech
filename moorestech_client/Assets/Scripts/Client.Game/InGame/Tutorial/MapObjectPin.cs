using System;
using System.Linq;
using Client.Game.InGame.Control;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Player;
using Cysharp.Threading.Tasks;
using TMPro;
using UniRx;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Tutorial
{
    public class MapObjectPin : MonoBehaviour
    {
        [SerializeField] private TMP_Text pinText;
        
        private InGameCameraController _inGameCameraController;
        private MapObjectGameObjectDatastore _mapObjectGameObjectDatastore;
        private IPlayerObjectController _playerObjectController;
        
        private IDisposable _mapObjectOnDestroy;
        
        [Inject]
        public void Construct(InGameCameraController inGameCameraController, MapObjectGameObjectDatastore mapObjectGameObjectDatastore, IPlayerObjectController playerObjectController)
        {
            _inGameCameraController = inGameCameraController;
            _mapObjectGameObjectDatastore = mapObjectGameObjectDatastore;
            _playerObjectController = playerObjectController;
        }
        
        private void Update()
        {
            // Y軸を常にカメラに向ける
            transform.LookAt(_inGameCameraController.Position);
            transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
        }
        
        public void SetNearMapObject(string mapObjectType)
        {
            _mapObjectOnDestroy?.Dispose();
            
            var mapObjects = _mapObjectGameObjectDatastore.CreateMapObjectList(mapObjectType);
            var playerPos = _playerObjectController.Position;
            var nearMapObject = mapObjects.OrderBy(x => (playerPos - x.GetPosition()).sqrMagnitude).First();
            gameObject.SetActive(true);
            transform.position = nearMapObject.GetPosition();
            
            _mapObjectOnDestroy = nearMapObject.OnDestroyMapObject.Subscribe(_ => gameObject.SetActive(false)).AddTo(this);
        }
        
        public void SetText(string text)
        {
            pinText.text = text;
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
    }
}