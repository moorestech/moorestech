using System;
using System.Linq;
using Client.Game.InGame.Control;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Player;
using Game.Challenge;
using Game.Challenge.Config.TutorialParam;
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
            var mapObjectParam = (MapObjectPinTutorialParam)param;
            
            _mapObjectOnDestroy?.Dispose();
            
            // 近くのMapObjectを探してピンを表示
            var mapObjects = _mapObjectGameObjectDatastore.CreateMapObjectList(mapObjectParam.MapObjectType);
            var playerPos = _playerObjectController.Position;
            var sortedMapObjects = mapObjects.OrderBy(x => (playerPos - x.GetPosition()).sqrMagnitude).ToList();
            if (sortedMapObjects.Count == 0)
            {
                Debug.LogWarning($"未破壊のMapObject {mapObjectParam.MapObjectType} が存在しません");
                return null;
            }
            
            var nearMapObject = sortedMapObjects.First();
            transform.position = nearMapObject.GetPosition();
            
            // そのMapObjectが破壊されたらピンを非表示にする
            _mapObjectOnDestroy = nearMapObject.OnDestroyMapObject.Subscribe(_ => SetActive(false)).AddTo(this);
            
            // ピンのテキストを設定
            pinText.text = mapObjectParam.PinText;
            
            SetActive(true);
            
            return this;
        }
        
        public void CompleteTutorial()
        {
            SetActive(false);
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
    }
}