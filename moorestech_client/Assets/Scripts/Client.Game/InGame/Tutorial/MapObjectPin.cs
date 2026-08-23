using System;
using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Control;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.UIState;
using Core.Master;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Tutorial
{
    public class MapObjectPin : MonoBehaviour, ITutorialWorldPin
    {
        // WebオーバーレイでのピンID。MapObjectPinはシーンに1つなので固定IDでよい
        // World-pin id on the web overlay; a single scene instance suffices, so the id is fixed
        private const string WebPinId = "map-object-pin";

        // 未適用・完了後の空候補。毎フレームの探索でnull分岐を持たずに済む
        // Empty candidates before apply and after completion, so the per-frame search needs no null branch
        private static readonly HashSet<Guid> EmptyTargets = new HashSet<Guid>();

        private InGameCameraController _inGameCameraController;
        private MapObjectGameObjectDatastore _mapObjectGameObjectDatastore;
        private TutorialWorldPinVisibility _visibility;

        // ピンは非活性で置かれAwakeが走らないまま表示要求が届くため、初回要求時に組み立てる
        // A pin can sit inactive with no Awake yet still receive a visibility request, so build it on first use
        private TutorialWorldPinVisibility Visibility => _visibility ??= new TutorialWorldPinVisibility(gameObject, nameof(MapObjectPin));

        private MapObjectPinTutorialParam _currentTutorialParam;
        private HashSet<Guid> _targetMapObjectGuids = EmptyTargets;
        private string _pinTutorialGuid = "";

        // 候補全滅は毎フレーム出すとログを埋めるので、対象が変わるまで初回だけ報告する
        // Reporting "no candidate left" every frame would bury the log, so report once until the target changes
        private bool _missingReported;

        [Inject]
        public void Construct(InGameCameraController inGameCameraController, MapObjectGameObjectDatastore mapObjectGameObjectDatastore)
        {
            _inGameCameraController = inGameCameraController;
            _mapObjectGameObjectDatastore = mapObjectGameObjectDatastore;
        }

        private void Update()
        {
            if (_currentTutorialParam == null) return;

            // Y軸を常にカメラに向ける
            // Face the camera on the Y axis only
            transform.LookAt(_inGameCameraController.Position);
            transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);

            // 追えなければ非表示済みなので配信しない
            // Nothing to publish once tracking failed and the pin got hidden
            if (!TryTrackNearestMapObject()) return;
            PublishWebWorldPin();

            #region Internal

            bool TryTrackNearestMapObject()
            {
                // 候補集合中の最寄り未破壊にピン
                // Pin the nearest undestroyed candidate
                var playerPos = PlayerSystemContainer.Instance.PlayerObjectController.Position;
                var mapObject = _mapObjectGameObjectDatastore.SearchNearestMapObject(_targetMapObjectGuids, playerPos);

                if (mapObject == null)
                {
                    HideForMissingMapObject();
                    return false;
                }

                transform.position = mapObject.GetPosition();
                return true;
            }

            void HideForMissingMapObject()
            {
                // 指す先無しは非表示、報告は初回のみ
                // Hide when there is nothing to point at; report only once
                if (!_missingReported)
                {
                    _missingReported = true;
                    Debug.LogError($"未破壊のMapObject（tutorialGuid={_pinTutorialGuid}、候補{_targetMapObjectGuids.Count}件）が存在しません");
                }

                SetActive(false);
                WorldPinStateStore.Instance.RemovePin(WebPinId);
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
            _currentTutorialParam = (MapObjectPinTutorialParam)tutorial.TutorialParam;
            _targetMapObjectGuids = MasterHolder.ChallengeMaster.ResolvePinTargets(_currentTutorialParam);
            _pinTutorialGuid = tutorial.TutorialGuid.ToString("D");
            _missingReported = false;

            // 追跡と射影配信のみ行う（表示はWebオーバーレイが担う）
            // Only tracking and projection publishing happen here; display lives on the web overlay
            SetActive(true);

            return this;
        }

        public void CompleteTutorial()
        {
            SetActive(false);
            _currentTutorialParam = null;
            _targetMapObjectGuids = EmptyTargets;
            WorldPinStateStore.Instance.RemovePin(WebPinId);
        }

        public string TutorialType => TutorialsElement.TutorialTypeConst.mapObjectPin;

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
