using System;
using System.Linq;
using Client.Common;
using Client.Game.InGame.Control;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.UIState;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Tutorial
{
    public interface IMapObjectPin : ITutorialViewManager, ITutorialView
    {
        public void SetActive(bool active);
        public void SetSkitSuppressed(bool suppressed);
        public bool IsSkitSuppressed();
    }
    
    public class MapObjectPin : MonoBehaviour, IMapObjectPin
    {
        // WebオーバーレイでのピンID。MapObjectPinはシーンに1つなので固定IDでよい
        // World-pin id on the web overlay; a single scene instance suffices, so the id is fixed
        private const string WebPinId = "map-object-pin";

        private InGameCameraController _inGameCameraController;
        private MapObjectGameObjectDatastore _mapObjectGameObjectDatastore;

        private MapObjectPinTutorialParam _currentTutorialParam;
        private string _pinTutorialGuid = "";
        private bool _desiredActive;
        private bool _skitSuppressed;
        private bool _visibilityInitialized;

        [Inject]
        public void Construct(InGameCameraController inGameCameraController, MapObjectGameObjectDatastore mapObjectGameObjectDatastore)
        {
            _inGameCameraController = inGameCameraController;
            _mapObjectGameObjectDatastore = mapObjectGameObjectDatastore;
        }

        private void Update()
        {
            // Y軸を常にカメラに向ける
            transform.LookAt(_inGameCameraController.Position);
            transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);

            // 最も近いMapObjectにピンする
            NearestPinMapObject();

            // Webへ射影配信する
            // Project and publish to the web overlay
            PublishWebWorldPin();

            #region Internal

            void PublishWebWorldPin()
            {
                if (!WebUiScreenGate.IsWebUiMode || _currentTutorialParam == null) return;

                var camera = CameraManager.MainCamera.Camera;
                if (!camera) return;

                var projection = WorldPinScreenProjection.Project(camera, transform.position);
                WorldPinStateStore.Instance.SetPin(WebPinId, _pinTutorialGuid, projection);
            }

            void NearestPinMapObject()
            {
                // 近くのMapObjectを探してピンを表示
                var playerPos = PlayerSystemContainer.Instance.PlayerObjectController.Position;
                var mapObject = _mapObjectGameObjectDatastore.SearchNearestMapObject(_currentTutorialParam.MapObjectGuid, playerPos);
                
                if (mapObject == null)
                {
                    Debug.LogError($"未破壊のMapObject {_currentTutorialParam.MapObjectGuid} が存在しません");
                    return;
                }
                
                transform.position = mapObject.GetPosition();
            }
            
            #endregion
        }
        
        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            _currentTutorialParam = (MapObjectPinTutorialParam)tutorial.TutorialParam;
            _pinTutorialGuid = tutorial.TutorialGuid.ToString("D");

            // 追跡と射影配信のみ行う（表示はWebオーバーレイが担う）
            // Only tracking and projection publishing happen here; display lives on the web overlay
            SetActive(true);

            return this;
        }

        public void CompleteTutorial()
        {
            SetActive(false);
            _currentTutorialParam = null;
            WorldPinStateStore.Instance.RemovePin(WebPinId);
        }

        public void SetActive(bool active)
        {
            _desiredActive = active;
            _visibilityInitialized = true;
            ApplyVisibility();
        }

        public void SetSkitSuppressed(bool suppressed)
        {
            EnsureDesiredActiveInitialized();
            _skitSuppressed = suppressed;
            ApplyVisibility();
        }

        public bool IsSkitSuppressed()
        {
            return _skitSuppressed;
        }

        private void EnsureDesiredActiveInitialized()
        {
            if (_visibilityInitialized) return;
            _desiredActive = gameObject.activeSelf;
            _visibilityInitialized = true;
        }

        private void ApplyVisibility()
        {
            gameObject.SetActive(_desiredActive && !_skitSuppressed);
        }

        // スキットの一時抑止でもWebピンを確実に消す（RemovePinは冪等）
        // Temporary skit suppression must also clear the web pin; RemovePin is idempotent
        private void OnDisable()
        {
            WorldPinStateStore.Instance.RemovePin(WebPinId);
        }

        private void OnDestroy()
        {
            WorldPinStateStore.Instance.RemovePin(WebPinId);
        }
    }
}
