using System;
using Client.Common;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.UIState;
using Mooresmaster.Model.ChallengesModule;
using UniRx;
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
        private Guid _reportedMissingMapObjectGuid;

        // 起動待機は近傍だけで明けるため、後着完了までは探索の空振りが「まだ生成されていない」を意味する
        // The startup wait only covers the near field, so until the background stream finishes a miss means "not yet instantiated"
        private bool _isAllMapObjectInstantiated;

        [Inject]
        public void Construct(MapObjectGameObjectDatastore mapObjectGameObjectDatastore)
        {
            _mapObjectGameObjectDatastore = mapObjectGameObjectDatastore;
            mapObjectGameObjectDatastore.IsAllInstantiated.Subscribe(isAllInstantiated => _isAllMapObjectInstantiated = isAllInstantiated).AddTo(this);
        }

        private void Update()
        {
            // 最も近いMapObjectにピンする
            var isTargetResolved = TryPinNearestMapObject();

            // Webへ射影配信する
            // Project and publish to the web overlay
            PublishWebWorldPin(isTargetResolved);

            #region Internal

            void PublishWebWorldPin(bool isResolved)
            {
                if (!WebUiScreenGate.IsWebUiMode || _currentTutorialParam == null) return;

                // 未解決の間に配信すると前回チュートリアルの座標を指し続ける。消してから解決を待つ
                // Publishing while unresolved keeps pointing at the previous tutorial's position, so clear the pin and wait for a resolution
                if (!isResolved)
                {
                    WorldPinStateStore.Instance.RemovePin(WebPinId);
                    return;
                }

                var camera = CameraManager.MainCamera.Camera;
                if (!camera) return;

                var projection = WorldPinScreenProjection.Project(camera, transform.position);
                WorldPinStateStore.Instance.SetPin(WebPinId, _pinTutorialGuid, projection);
            }

            bool TryPinNearestMapObject()
            {
                // 近くのMapObjectを探してピンを表示
                var playerPos = PlayerSystemContainer.Instance.PlayerObjectController.Position;
                var mapObject = _mapObjectGameObjectDatastore.SearchNearestMapObject(_currentTutorialParam.MapObjectGuid, playerPos);

                if (mapObject == null)
                {
                    // 後着途中の空振りは欠落ではないので報告もラッチもしない。誤報でラッチを消費すると本物の欠落が二度と出なくなる
                    // A miss mid-stream is not a missing target: reporting it would burn the latch and silence the real absence forever
                    if (!_isAllMapObjectInstantiated) return false;

                    if (_reportedMissingMapObjectGuid != _currentTutorialParam.MapObjectGuid)
                    {
                        _reportedMissingMapObjectGuid = _currentTutorialParam.MapObjectGuid;
                        Debug.LogError($"未破壊のMapObject {_currentTutorialParam.MapObjectGuid} が存在しません");
                    }
                    return false;
                }

                transform.position = mapObject.Position;
                return true;
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
