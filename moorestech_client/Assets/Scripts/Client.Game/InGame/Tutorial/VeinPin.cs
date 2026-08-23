using System;
using Client.Common;
using Client.Game.InGame.Map.Outcrop;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.UIState;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Tutorial
{
    public class VeinPin : MonoBehaviour, ITutorialWorldPin
    {
        // 専用IDでmapObjectピンの掃除との干渉を防ぐ
        // A separate ID prevents map-object cleanup from removing the vein pin
        private const string WebPinId = "vein-pin";

        private OutcropGameObjectDatastore _outcropGameObjectDatastore;
        private TutorialWorldPinVisibility _visibility;

        // ピンは非活性で置かれAwakeが走らないまま表示要求が届くため、初回要求時に組み立てる
        // A pin can sit inactive with no Awake yet still receive a visibility request, so build it on first use
        private TutorialWorldPinVisibility Visibility => _visibility ??= new TutorialWorldPinVisibility(gameObject, nameof(VeinPin));
        private VeinPinTutorialParam _currentTutorialParam;
        private string _pinTutorialGuid = "";

        // 露頭不在は毎フレーム出すとログを埋めるので、対象1件につき1回だけ報告する
        // Reporting a missing outcrop every frame would bury the log, so report once per target
        private bool _hasReportedMissingOutcropVeinGuid;
        private Guid _reportedMissingOutcropVeinGuid;

        // 配信中かどうか。露頭を失った後にRemovePinを毎フレーム叩き続けないための番人
        // Whether a pin is being published; guards RemovePin from running every frame after the outcrop is lost
        private bool _publishing;

        [Inject]
        public void Initialize(OutcropGameObjectDatastore outcropGameObjectDatastore)
        {
            _outcropGameObjectDatastore = outcropGameObjectDatastore;
        }

        private void Update()
        {
            if (_currentTutorialParam == null) return;

            if (!TryTrackNearestOutcrop()) return;
            PublishWebWorldPin();

            #region Internal

            bool TryTrackNearestOutcrop()
            {
                var playerPosition = PlayerSystemContainer.Instance.PlayerObjectController.Position;
                var outcrop = _outcropGameObjectDatastore.SearchNearestOutcrop(
                    _currentTutorialParam.VeinGuid,
                    playerPosition);
                if (outcrop == null)
                {
                    HideForMissingOutcrop();
                    return false;
                }

                _hasReportedMissingOutcropVeinGuid = false;
                transform.position = outcrop.transform.position;
                return true;
            }

            void HideForMissingOutcrop()
            {
                // 指す先が無いピンは配信を止め、報告は対象ごとに初回だけにする
                // Stop publishing a pin with nothing to point at, and report only the first time per target
                if (!_hasReportedMissingOutcropVeinGuid || _reportedMissingOutcropVeinGuid != _currentTutorialParam.VeinGuid)
                {
                    _hasReportedMissingOutcropVeinGuid = true;
                    _reportedMissingOutcropVeinGuid = _currentTutorialParam.VeinGuid;
                    Debug.LogError($"veinGuid:{_currentTutorialParam.VeinGuid}の露頭が存在しません");
                }

                // SetActive(false)はUpdateごと止めて対象が戻っても復帰できないため、配信の停止だけに留める
                // SetActive(false) would stop Update itself and never recover when a target returns, so only publishing is stopped
                if (!_publishing) return;
                RemoveWorldPin();
            }

            void PublishWebWorldPin()
            {
                if (!WebUiScreenGate.IsWebUiMode) return;
                var camera = CameraManager.MainCamera.Camera;
                if (!camera) return;

                var projection = WorldPinScreenProjection.Project(camera, transform.position);
                WorldPinStateStore.Instance.SetPin(WebPinId, _pinTutorialGuid, projection);
                _publishing = true;
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
            RemoveWorldPin();
        }

        private void RemoveWorldPin()
        {
            _publishing = false;
            WorldPinStateStore.Instance.RemovePin(WebPinId);
        }

        public string TutorialType => TutorialsElement.TutorialTypeConst.veinPin;

        public void SetActive(bool active)
        {
            Visibility.SetActive(active);
        }

        public void BeginSkitSuppress()
        {
            Visibility.BeginSkitSuppress();
        }

        public void EndSkitSuppress()
        {
            Visibility.EndSkitSuppress();
        }

        private void OnDisable()
        {
            RemoveWorldPin();
        }

        private void OnDestroy()
        {
            RemoveWorldPin();
        }
    }
}
