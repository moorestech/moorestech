using System;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.View;
using Client.Game.InGame.Train.View.Object.Material;
using Client.Game.InGame.Train.View.Object.Pose;
using Client.Game.InGame.Train.View.Object.Processors;
using Common.Debug;
using Game.Train.Unit;
using Mooresmaster.Model.TrainModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.Train.View.Object.Core
{
    [RequireComponent(typeof(Rigidbody))]
    public class TrainCarEntityObject : MonoBehaviour
    {
        public TrainCarInstanceId TrainCarInstanceId { get; private set; }

        private TrainCarMasterElement _trainCarMasterElement;
        private ITrainCarPoseUpdater _poseUpdater;
        private ITrainCarObjectProcessor[] _processors = Array.Empty<ITrainCarObjectProcessor>();
        private TrainCarMaterialController _materialController;
        private static bool _isDebugAutoRunInitialized;
        private static bool _debugAutoRun;

        public void Initialize(TrainCarInstanceId trainCarInstanceId, TrainCarMasterElement trainCarMasterElement)
        {
            // 通常描画 entity に必要な ID と master 情報だけを保持する
            // Keep only the id and master data required by the runtime entity
            TrainCarInstanceId = trainCarInstanceId;
            _trainCarMasterElement = trainCarMasterElement;

            // 乗車と接触判定用の Rigidbody だけを初期化する
            // Initialize Rigidbody for contact and riding detection
            var rigidbody = GetComponent<Rigidbody>();
            ConfigureRigidbodyForContact(rigidbody);

            #region Internal

            void ConfigureRigidbodyForContact(Rigidbody targetRigidbody)
            {
                // Rigidbody は接触判定に限定し、車両姿勢は Transform 更新で決める
                // Restrict Rigidbody to contact detection while train pose stays Transform-driven
                targetRigidbody.isKinematic = true;
                targetRigidbody.useGravity = false;
                targetRigidbody.interpolation = RigidbodyInterpolation.None;
                targetRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }

            #endregion
        }

        public TrainCarMasterElement GetTrainCarMasterElement()
        {
            return _trainCarMasterElement;
        }

        public void SetPoseUpdater(ITrainCarPoseUpdater poseUpdater)
        {
            // Prefab 上の再帰 pose updater と通常描画 processor を entity に保持する
            // Store the recursive Prefab pose updater and runtime processors on the entity
            _poseUpdater = poseUpdater;
            _processors = GetComponentsInChildren<ITrainCarObjectProcessor>();
            for (var i = 0; i < _processors.Length; i++)
            {
                _processors[i].Initialize(this);
            }
        }

        public void SetMaterialController(TrainCarMaterialController materialController)
        {
            // entity 外からの一時ハイライト要求を material controller へ集約する
            // Store the material controller for entity-level temporary highlight requests
            _materialController = materialController;
        }

        public void RequestOverlayForCurrentFrame(TrainCarVisualMaterialMode materialMode)
        {
            // 設置 snap など entity 外の要求を現在フレームの overlay として渡す
            // Forward external placement-snap requests as current-frame overlays
            _materialController.RequestOverlayForCurrentFrame(materialMode);
        }

        public bool ApplyVisualState(TrainCarRailPositionVisualState visualState, TrainCarContext context)
        {
            // material overlay の期限を整理してから pose を適用する
            // Expire material overlays before applying pose
            _materialController.RefreshCurrentFrameOverlay();
            if (!_poseUpdater.UpdatePose(visualState))
            {
                DispatchProcessors(TrainCarContext.CreateUnavailable());
                return false;
            }

            // pose 適用に成功した時だけ processor へ描画 context を配る
            // Dispatch render context to processors only after pose application succeeds
            DispatchProcessors(context);
            return true;
        }

        public bool CollectVisualPoseRequests(TrainCarRailPositionVisualState visualState, TrainCarRailPositionPoseBatch poseBatch)
        {
            // unit単位batchへ、このentity配下の全visual spanを登録する
            // Register every visual span under this entity into the unit-level batch
            return _poseUpdater.CollectPoseRequests(visualState, poseBatch);
        }

        public bool ApplyBatchedVisualState(TrainCarRailPositionVisualState visualState, TrainCarContext context, TrainCarRailPositionPoseBatch poseBatch)
        {
            // material overlayの期限処理はTransform適用直前に従来通り行う
            // Keep material overlay expiry immediately before applying the Transform state
            _materialController.RefreshCurrentFrameOverlay();
            if (!_poseUpdater.ApplyBatchedPose(visualState, poseBatch))
            {
                DispatchProcessors(TrainCarContext.CreateUnavailable());
                return false;
            }

            // batch pose適用に成功したentityだけ通常描画contextを流す
            // Dispatch render context only for entities whose batched pose application succeeded
            DispatchProcessors(context);
            return true;
        }

        public void ApplyUnavailableVisualState()
        {
            // 描画 snapshot が使えない場合も overlay 期限と processor 状態を更新する
            // Update overlay expiry and processor state even when no render snapshot is available
            _materialController.RefreshCurrentFrameOverlay();
            DispatchProcessors(TrainCarContext.CreateUnavailable());
        }

        public void Destroy()
        {
            // entity 破棄前に runtime material を解放する
            // Release runtime materials before destroying the entity object
            _materialController.DestroyRuntimeMaterials();
            Destroy(gameObject);
        }

        private void Update()
        {
            // debug auto-run command は TrainCarEntityObject 内に残し、描画更新は unit visual updater 側で行う
            // Keep the debug auto-run command here while visual updates stay in the unit visual updater
            UpdateDebugAutoRunCommand();
        }

        private void DispatchProcessors(TrainCarContext context)
        {
            // 通常描画専用 processor へ同じ context を配る
            // Dispatch the same context to all runtime-only processors
            for (var i = 0; i < _processors.Length; i++)
            {
                _processors[i].Update(context);
            }
        }

        private static void UpdateDebugAutoRunCommand()
        {
            var currentDebugAutoRun = DebugParameters.GetValueOrDefaultBool(DebugConst.TrainAutoRunKey);
            if (!_isDebugAutoRunInitialized)
            {
                // 複数 car entity があっても初期状態は共有して一度だけ記録する
                // Share the initial state so multiple car entities do not send duplicate commands
                _debugAutoRun = currentDebugAutoRun;
                _isDebugAutoRunInitialized = true;
                return;
            }
            if (_debugAutoRun == currentDebugAutoRun)
            {
                return;
            }

            // toggle 変化時だけサーバーへ全列車の自動運転切り替えを送る
            // Send the all-train auto-run command only when the toggle changes
            _debugAutoRun = currentDebugAutoRun;
            SendTrainAutoRunChanged(_debugAutoRun);
            UnityEngine.Debug.Log($"[Debug] Train auto run changed: {_debugAutoRun}");
        }

        private static void SendTrainAutoRunChanged(bool isEnabled)
        {
            // サーバーへ全列車の自動運転切り替えコマンドを送信する
            // Send the auto-run toggle command for all trains to the server
            var command = isEnabled
                ? $"{SendCommandProtocol.TrainAutoRunCommand} {SendCommandProtocol.TrainAutoRunOnArgument}"
                : $"{SendCommandProtocol.TrainAutoRunCommand} {SendCommandProtocol.TrainAutoRunOffArgument}";
            ClientContext.VanillaApi.SendOnly.SendCommand(command);
        }
    }
}
