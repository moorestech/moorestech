using Client.Game.InGame.Context;
using Client.Game.InGame.Train.View;
using Common.Debug;
using Game.Train.Unit;
using Mooresmaster.Model.TrainModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.Train.View.Object
{
    [RequireComponent(typeof(Rigidbody))]
    public class TrainCarEntityObject : MonoBehaviour
    {
        public TrainCarInstanceId TrainCarInstanceId { get; private set; }

        private bool _debugAutoRun;
        private TrainCarMasterElement _trainCarMasterElement;
        private TrainCarEntityRenderInterpolator _renderInterpolator;
        private TrainCarMaterialController _materialController;

        public void Initialize(TrainCarInstanceId trainCarInstanceId, TrainCarMasterElement trainCarMasterElement)
        {
            // 通常描画 entity に必要な ID と master 情報だけを保持する
            // Keep only the id and master data required by the runtime entity
            TrainCarInstanceId = trainCarInstanceId;
            _trainCarMasterElement = trainCarMasterElement;
            _debugAutoRun = DebugParameters.GetValueOrDefaultBool(DebugConst.TrainAutoRunKey);

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

        public void SetMaterialController(TrainCarMaterialController materialController)
        {
            // entity 外からの一時ハイライト要求を material controller へ集約する
            // Store the material controller for entity-level temporary highlight requests
            _materialController = materialController;
        }

        public void SetRenderInterpolator(TrainCarEntityRenderInterpolator renderInterpolator)
        {
            // 通常描画 driver は entity の ID 確定後に差し込む
            // Attach the runtime render driver after the entity id is fixed
            _renderInterpolator = renderInterpolator;
        }

        public void RequestOverlayForCurrentFrame(TrainCarVisualMaterialMode materialMode)
        {
            // 設置 snap など entity 外の要求を現在フレームの overlay として渡す
            // Forward external placement-snap requests as current-frame overlays
            _materialController.RequestOverlayForCurrentFrame(materialMode);
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
            // TODO: 将来は TrainUnit 単位の visual updater を作り、unit の RailPosition と Cars から各 car の visualState を一括生成する。
            // TODO: Create a TrainUnit-level visual updater that builds each car visualState from the unit RailPosition and Cars together.
            // TODO: その時はこの MonoBehaviour.Update を消し、entity は material/pose/processor 適用 API だけを持つ受け身にする。
            // TODO: Then remove this MonoBehaviour.Update and keep the entity as a passive API for material, pose, and processor application.

            // material overlay の期限を整理してから pose と processor を更新する
            // Expire material overlays before updating pose and processors
            _materialController.RefreshCurrentFrameOverlay();
            _renderInterpolator.Update();

            // デバッグ用の自動運転切り替えが変わった時だけサーバーへ通知する
            // Notify the server only when the debug train AutoRun state changes
            if (_debugAutoRun == DebugParameters.GetValueOrDefaultBool(DebugConst.TrainAutoRunKey))
            {
                return;
            }

            _debugAutoRun = DebugParameters.GetValueOrDefaultBool(DebugConst.TrainAutoRunKey);
            OnTrainAutoRunChanged(_debugAutoRun);
            Debug.Log($"[Debug] Train auto run changed: {_debugAutoRun}");
        }

        private void OnTrainAutoRunChanged(bool isEnabled)
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
