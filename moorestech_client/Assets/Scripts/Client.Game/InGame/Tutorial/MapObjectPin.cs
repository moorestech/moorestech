using System;
using Client.Common;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.UIState;
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

        private MapObjectGameObjectDatastore _mapObjectGameObjectDatastore;
        private TutorialWorldPinVisibility _visibility;

        // ピンは非活性で置かれAwakeが走らないまま表示要求が届くため、初回要求時に組み立てる
        // A pin can sit inactive with no Awake yet still receive a visibility request, so build it on first use
        private TutorialWorldPinVisibility Visibility => _visibility ??= new TutorialWorldPinVisibility(gameObject, nameof(MapObjectPin));

        private MapObjectPinTutorialParam _currentTutorialParam;
        private string _pinTutorialGuid = "";

        // 対象不在は毎フレーム出すとログを埋めるので、対象1件につき1回だけ報告する（VeinPinと同形）
        // Reporting a missing target every frame would bury the log, so report once per target (same as VeinPin)
        private bool _hasReportedMissingMapObjectGuid;
        private Guid _reportedMissingMapObjectGuid;

        [Inject]
        public void Initialize(MapObjectGameObjectDatastore mapObjectGameObjectDatastore)
        {
            _mapObjectGameObjectDatastore = mapObjectGameObjectDatastore;
        }

        private void Update()
        {
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
                var playerPos = PlayerSystemContainer.Instance.PlayerObjectController.Position;
                var mapObject = _mapObjectGameObjectDatastore.SearchNearestMapObject(_currentTutorialParam.MapObjectGuid, playerPos);

                if (mapObject == null)
                {
                    if (!_hasReportedMissingMapObjectGuid || _reportedMissingMapObjectGuid != _currentTutorialParam.MapObjectGuid)
                    {
                        _hasReportedMissingMapObjectGuid = true;
                        _reportedMissingMapObjectGuid = _currentTutorialParam.MapObjectGuid;
                        Debug.LogError($"未破壊のMapObject {_currentTutorialParam.MapObjectGuid} が存在しません");
                    }

                    // 指す先が無い間は古い座標の配信を止める。Updateは回し続けるので対象が戻れば追従を再開する
                    // Stop publishing a stale position while there is nothing to point at; Update keeps running so tracking resumes when a target returns
                    WorldPinStateStore.Instance.RemovePin(WebPinId);
                    return;
                }

                _hasReportedMissingMapObjectGuid = false;
                transform.position = mapObject.Position;
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
