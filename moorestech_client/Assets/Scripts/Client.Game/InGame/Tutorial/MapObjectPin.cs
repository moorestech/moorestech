using System;
using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.UIState;
using Core.Master;
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

        // 未適用・完了後の空候補。毎フレームの探索でnull分岐を持たずに済む
        // Empty candidates before apply and after completion, so the per-frame search needs no null branch
        private static readonly HashSet<Guid> EmptyTargets = new HashSet<Guid>();

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

        // 配信中かどうか。指す先を失った後にRemovePinを毎フレーム叩き続けないための番人
        // Whether a pin is being published; guards RemovePin from running every frame after the target is lost
        private bool _publishing;

        // 起動待機は近傍だけで明けるため、後着完了までは探索の空振りが「まだ生成されていない」を意味する
        // The startup wait only covers the near field, so until the background stream finishes a miss means "not yet instantiated"
        private bool _isAllMapObjectInstantiated;

        [Inject]
        public void Initialize(MapObjectGameObjectDatastore mapObjectGameObjectDatastore)
        {
            _mapObjectGameObjectDatastore = mapObjectGameObjectDatastore;
            mapObjectGameObjectDatastore.IsAllInstantiated.Subscribe(isAllInstantiated => _isAllMapObjectInstantiated = isAllInstantiated).AddTo(this);
        }

        private void Update()
        {
            if (_currentTutorialParam == null) return;

            // 追えなければ配信するべき座標が無いので止める
            // Nothing to publish when tracking failed, so stop here
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

                _missingReported = false;
                transform.position = mapObject.transform.position;
                return true;
            }

            void HideForMissingMapObject()
            {
                // 指す先無しのまま配信を続けると前回チュートリアルの座標を指し続ける。消すのは配信中の1回だけ
                // Publishing on with nothing to point at keeps showing the previous tutorial's position; the removal runs only once per publishing streak
                if (_publishing) RemoveWorldPin();

                // 後着途中の空振りは欠落ではないので報告もラッチもしない。誤報でラッチを消費すると本物の欠落が二度と出なくなる
                // A miss mid-stream is not a missing target: reporting it would burn the latch and silence the real absence forever
                if (!_isAllMapObjectInstantiated) return;
                if (_missingReported) return;

                _missingReported = true;
                Debug.LogError($"未破壊のMapObject（tutorialGuid={_pinTutorialGuid}、候補{_targetMapObjectGuids.Count}件）が存在しません");
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
            RemoveWorldPin();
        }

        private void RemoveWorldPin()
        {
            _publishing = false;
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

        // スキットの一時抑止でもWebピンを確実に消す
        // Temporary skit suppression must also clear the web pin
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
