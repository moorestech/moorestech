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
        public void SetSkitSuppressed(bool suppressed);
        public bool IsSkitSuppressed();
    }

    public class VeinPin : MonoBehaviour, IVeinPin
    {
        // 独立したWebピンID
        // Independent web-pin ID
        private const string WebPinId = "vein-pin";

        private InGameCameraController _inGameCameraController;
        private OutcropGameObjectDatastore _outcropGameObjectDatastore;
        private VeinPinTutorialParam _currentTutorialParam;
        private string _pinTutorialGuid = "";
        private bool _desiredActive;
        private bool _skitSuppressed;
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

        public void SetSkitSuppressed(bool suppressed)
        {
            EnsureDesiredActiveInitialized();
            _skitSuppressed = suppressed;
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

        public bool IsSkitSuppressed()
        {
            return _skitSuppressed;
        }

        private void ApplyVisibility()
        {
            gameObject.SetActive(_desiredActive && !_skitSuppressed);
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
