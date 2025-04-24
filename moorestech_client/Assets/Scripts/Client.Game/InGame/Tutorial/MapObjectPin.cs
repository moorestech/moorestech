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
            
            // 最も近いMapObjectにピンする
            NearestPinMapObject();
            
            #region Internal
            
            void NearestPinMapObject()
            {
                // 近くのMapObjectを探してピンを表示
                var playerPos = _playerObjectController.Position;
                var mapObject = _mapObjectGameObjectDatastore.SearchNearestMapObject(_currentTutorialParam.MapObjectGuid, playerPos);
                
                if (mapObject == null)
                {
                    Debug.LogWarning($"未破壊のMapObject {_currentTutorialParam.MapObjectGuid} が存在しません");
                }
                
                transform.position = mapObject.GetPosition();
            }
            
  #endregion
        }
        
        public ITutorialView ApplyTutorial(ITutorialParam param)
        {
            _currentTutorialParam = (MapObjectPinTutorialParam)param;
            
            
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