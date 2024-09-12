using System;
using System.Linq;
using Client.Game.InGame.Control;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Player;
using Mooresmaster.Model.ChallengesModule;
using TMPro;
using UniRx;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Tutorial
{
    public class MapObjectPin : MonoBehaviour, ITutorialView, ITutorialViewManager
    {
        [SerializeField] private TMP_Text pinText;
        
        private InGameCameraController _inGameCameraController;
        private MapObjectGameObjectDatastore _mapObjectGameObjectDatastore;
        private IPlayerObjectController _playerObjectController;
        
        private MapObjectPinTutorialParam _currentTutorialParam;
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
        
        public ITutorialView ApplyTutorial(ITutorialParam param)
        {
            _currentTutorialParam = (MapObjectPinTutorialParam)param;
            
            _mapObjectOnDestroy?.Dispose();
            
            // 近くのMapObjectを探してピンを表示
            var mapObjects = _mapObjectGameObjectDatastore.CreateMapObjectList(_currentTutorialParam.MapObjectType);
            var playerPos = _playerObjectController.Position;
            var sortedMapObjects = mapObjects.OrderBy(x => (playerPos - x.GetPosition()).sqrMagnitude).ToList();
            if (sortedMapObjects.Count == 0)
            {
                Debug.LogWarning($"未破壊のMapObject {_currentTutorialParam.MapObjectType} が存在しません");
                return null;
            }
            
            var nearMapObject = sortedMapObjects.First();
            transform.position = nearMapObject.GetPosition();
            
            // そのMapObjectが破壊されたら別のmapObjectを探す
            _mapObjectOnDestroy = nearMapObject.OnDestroyMapObject.Subscribe(_ =>
            {
                ApplyTutorial(_currentTutorialParam);
            }).AddTo(this);
            
            // ピンのテキストを設定
            pinText.text = _currentTutorialParam.PinText;
            
            SetActive(true);
            
            return this;
        }
        
        public void CompleteTutorial()
        {
            SetActive(false);
            _currentTutorialParam = null;
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
    }
}