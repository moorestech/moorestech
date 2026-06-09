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
        private ITrainCarVisualTarget _visualTarget;

        public void Initialize(TrainCarInstanceId trainCarInstanceId, TrainCarMasterElement trainCarMasterElement)
        {
            // 通常描画entityに必要なID、マスタ、描画駆動だけを保持する
            // Keep only the id, master data, and runtime render driver required by the entity
            TrainCarInstanceId = trainCarInstanceId;
            _trainCarMasterElement = trainCarMasterElement;
            _debugAutoRun = DebugParameters.GetValueOrDefaultBool(DebugConst.TrainAutoRunKey);

            // 乗車と接触判定用のRigidbodyだけを初期化する
            // Initialize Rigidbody for contact/riding
            var rigidbody = GetComponent<Rigidbody>();
            ConfigureRigidbodyForContact(rigidbody);

            #region Internal

            void ConfigureRigidbodyForContact(Rigidbody targetRigidbody)
            {
                // Rigidbodyは接触判定に限定し、列車姿勢はTransform更新で決める
                // Restrict Rigidbody to riding/contact detection while train pose stays Transform-driven
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

        public void SetVisualTarget(ITrainCarVisualTarget visualTarget)
        {
            // entity外からの一時ハイライト要求をvisual controllerへ中継できるよう保持する
            // Store the visual controller so entity-level callers can request temporary highlights
            _visualTarget = visualTarget;
        }

        public void SetRenderInterpolator(TrainCarEntityRenderInterpolator renderInterpolator)
        {
            // 通常描画driverはentityのID確定後に差し込む
            // Attach the runtime render driver after the entity id is fixed
            _renderInterpolator = renderInterpolator;
        }

        public void RequestOverlayForCurrentFrame(TrainCarVisualMaterialMode materialMode)
        {
            // 設置スナップなどentity外の要求を現在フレームの overlay として渡す
            // Forward external placement-snap requests as current-frame overlays
            _visualTarget.RequestOverlayForCurrentFrame(materialMode);
        }

        public void Destroy()
        {
            // entity破棄時に通常描画driverへruntime resourceの解放を委譲する
            // Delegate runtime resource release to the runtime render driver
            _renderInterpolator.DestroyRuntimeResources();
            Destroy(gameObject);
        }

        private void Update()
        {
            // 通常描画mode専用の補間、表示更新、processor dispatchを進める
            // Advance runtime-only interpolation, visual updates, and processor dispatch
            _renderInterpolator.Update();

            // デバッグ用の自動運転切り替えが変わった時だけサーバーへ通知する
            // Notify the server when the debug train AutoRun state changes
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
