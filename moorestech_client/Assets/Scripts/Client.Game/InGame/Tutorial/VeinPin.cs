using System;
using Client.Common;
using Client.Game.InGame.Control;
using Client.Game.InGame.Map.Outcrop;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.UIState;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Tutorial
{
    public interface IVeinPin : ITutorialViewManager, ITutorialView
    {
        public void SetActive(bool active);

        // 抑止は入れ子で始まり得るので、真偽の代入ではなく深さの増減で表す
        // Suppression can nest, so it is expressed as depth changes rather than assigning a flag
        public void BeginSkitSuppress();
        public void EndSkitSuppress();
    }

    public class VeinPin : MonoBehaviour, IVeinPin
    {
        // 専用IDでmapObjectピンの掃除との干渉を防ぐ
        // A separate ID prevents map-object cleanup from removing the vein pin
        private const string WebPinId = "vein-pin";

        private InGameCameraController _inGameCameraController;
        private OutcropGameObjectDatastore _outcropGameObjectDatastore;
        private VeinPinTutorialParam _currentTutorialParam;
        private string _pinTutorialGuid = "";
        private bool _desiredActive;
        private int _skitSuppressDepth;
        private bool _visibilityInitialized;

        [Inject]
        public void Initialize(InGameCameraController inGameCameraController, OutcropGameObjectDatastore outcropGameObjectDatastore)
        {
            _inGameCameraController = inGameCameraController;
            _outcropGameObjectDatastore = outcropGameObjectDatastore;
        }

        private void Update()
        {
            if (_currentTutorialParam == null) return;

            // Y軸だけカメラへ向ける
            // Face camera on Y axis only
            transform.LookAt(_inGameCameraController.Position);
            transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
            TrackNearestOutcrop();
            PublishWebWorldPin();

            #region Internal

            void TrackNearestOutcrop()
            {
                var playerPosition = PlayerSystemContainer.Instance.PlayerObjectController.Position;
                var outcrop = _outcropGameObjectDatastore.SearchNearestOutcrop(
                    _currentTutorialParam.VeinGuid,
                    playerPosition);
                if (outcrop == null)
                {
                    Debug.LogError($"veinGuid:{_currentTutorialParam.VeinGuid}の露頭が存在しません");
                    return;
                }

                transform.position = outcrop.transform.position;
            }

            void PublishWebWorldPin()
            {
                if (!WebUiScreenGate.IsWebUiMode) return;
                var camera = CameraManager.MainCamera.Camera;
                if (!camera) return;

                var projection = WorldPinScreenProjection.Project(camera, transform.position);
                WorldPinStateStore.Instance.SetPin(WebPinId, _pinTutorialGuid, projection);
            }

            #endregion
        }

        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            _currentTutorialParam = (VeinPinTutorialParam)tutorial.TutorialParam;
            _pinTutorialGuid = tutorial.TutorialGuid.ToString("D");
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

        public void BeginSkitSuppress()
        {
            EnsureDesiredActiveInitialized();
            _skitSuppressDepth++;
            ApplyVisibility();

            #region Internal

            void EnsureDesiredActiveInitialized()
            {
                if (_visibilityInitialized) return;
                _desiredActive = gameObject.activeSelf;
                _visibilityInitialized = true;
            }

            #endregion
        }

        public void EndSkitSuppress()
        {
            // 開始より多い解除は抑止が漏れた合図なので、0で止めず不整合として顕在化させる
            // More ends than begins signals a leaked suppression, so surface it instead of clamping at zero
            if (_skitSuppressDepth == 0)
                throw new InvalidOperationException("[VeinPin] BeginSkitSuppressより多くEndSkitSuppressが呼ばれました");

            _skitSuppressDepth--;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            gameObject.SetActive(_desiredActive && _skitSuppressDepth == 0);
        }

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
