using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
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
        public TrainCarMasterElement TrainCarMasterElement { get; set; }
        /// <summary>
        /// モデル中心の前後オフセット
        /// Model forward center offset
        /// </summary>
        public float ModelForwardCenterOffset => _poseService.ModelForwardCenterOffset;

        private bool _debugAutoRun = false;//////////////////
        private RendererMaterialReplacerController _rendererMaterialReplacerController;
        private TrainCarPoseService _poseService;
        private MaterialPreviewState _materialPreviewState = MaterialPreviewState.Normal;

        private enum MaterialPreviewState
        {
            Normal,
            RemovePreviewing,
            PlacementOverlapPreviewing,
        }

        /// <summary>
        /// 初期化を行う
        /// Perform initialization
        /// </summary>
        public void Initialize()
        {
            _debugAutoRun = DebugParameters.GetValueOrDefaultBool(DebugConst.TrainAutoRunKey);//////////////////
            _rendererMaterialReplacerController = new RendererMaterialReplacerController(gameObject);
            var rigidbody = GetComponent<Rigidbody>();
            ConfigureRigidbodyForContact(rigidbody);

            // 表示姿勢制御をサービスへ委譲する
            // Delegate render pose control to the service
            var renderers = GetComponentsInChildren<Renderer>(true);
            _poseService = new TrainCarPoseService(transform, renderers);

            #region Internal

            void ConfigureRigidbodyForContact(Rigidbody targetRigidbody)
            {
                // Rigidbodyは乗車・接触検出用に限定し、列車姿勢はTransform更新で決める。
                // Restrict the Rigidbody to riding/contact detection while train pose stays Transform-driven.
                targetRigidbody.isKinematic = true;
                targetRigidbody.useGravity = false;
                targetRigidbody.interpolation = RigidbodyInterpolation.None;
                targetRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }

            #endregion
        }

        public void SetTrain(TrainCarInstanceId trainCarInstanceId, TrainCarMasterElement trainCarMasterElement)
        {
            TrainCarInstanceId = trainCarInstanceId;
            TrainCarMasterElement = trainCarMasterElement;
        }

        /// <summary>
        /// 物理更新で反映する列車姿勢を設定する
        /// Set the train pose to apply during physics updates
        /// </summary>
        public void SetDirectPose(Vector3 position, Quaternion rotation)
        {
            _poseService.RequestPose(position, rotation);
        }

        /// <summary>
        /// GameObject を破棄する
        /// Destroy GameObject
        /// </summary>
        public void Destroy()
        {
            _rendererMaterialReplacerController?.DestroyMaterial();
            Destroy(gameObject);
        }

        /// <summary>
        /// 毎フレーム呼ばれ
        /// Called every frame
        /// </summary>
        private void Update()
        {
            // デバッグ用：列車の自動運転（AutoRun）の ON/OFF が変化したらサーバへ通知する
            // Notify the server when the debug train AutoRun state changes
            if (_debugAutoRun != DebugParameters.GetValueOrDefaultBool(DebugConst.TrainAutoRunKey))
            {
                _debugAutoRun = DebugParameters.GetValueOrDefaultBool(DebugConst.TrainAutoRunKey);
                OnTrainAutoRunChanged(_debugAutoRun);
                Debug.Log($"[Debug] Train auto run changed: {_debugAutoRun}");
            }
        }

        // 自動運転（AutoRun）の状態をサーバへ送信するローカル関数
        // Local function to send the auto-run state for all trains
        private void OnTrainAutoRunChanged(bool isEnabled)
        {
            // サーバへ「列車自動運転」の切り替えコマンドを送信する
            // Send the auto-run toggle command for all trains to the server
            var command = isEnabled
                ? $"{SendCommandProtocol.TrainAutoRunCommand} {SendCommandProtocol.TrainAutoRunOnArgument}"
                : $"{SendCommandProtocol.TrainAutoRunCommand} {SendCommandProtocol.TrainAutoRunOffArgument}";
            ClientContext.VanillaApi.SendOnly.SendCommand(command);
        }

        public void SetRemovePreviewing()
        {
            // 同じ削除プレビュー状態では材質差し替えを繰り返さない
            // Avoid repeated material replacement while already in remove preview
            if (_materialPreviewState != MaterialPreviewState.RemovePreviewing)
            {
                var placePreviewMaterial = MaterialConst.GetPreviewPlaceBlockMaterial();
                _rendererMaterialReplacerController.CopyAndSetMaterial(placePreviewMaterial);
                _materialPreviewState = MaterialPreviewState.RemovePreviewing;
            }
            _rendererMaterialReplacerController.SetColor(MaterialConst.PreviewColorPropertyName, MaterialConst.NotPlaceableColor);
        }

        // 事実上、新規でTrainCarを設置しようとしたときに連結できますよを視覚的に知らせるための表示のみ用
        // This preview only visually indicates that a new TrainCar can be connected
        public void SetPlacementOverlapPreviewing()
        {
            // 同じ設置重複ハイライトでは材質差し替えを繰り返さない
            // Avoid repeated material replacement while already in placement overlap preview
            if (_materialPreviewState != MaterialPreviewState.PlacementOverlapPreviewing)
            {
                var placePreviewMaterial = MaterialConst.GetPreviewPlaceBlockMaterial();
                _rendererMaterialReplacerController.CopyAndSetMaterial(placePreviewMaterial);
                _materialPreviewState = MaterialPreviewState.PlacementOverlapPreviewing;
            }
            // 設置候補重複ハイライトは設置可能色(青)で表示する
            // Show placement-overlap highlight in placeable color (blue)
            _rendererMaterialReplacerController.SetColor(MaterialConst.PreviewColorPropertyName, MaterialConst.PlaceableColor);
        }

        public void ResetMaterial()
        {
            if (_materialPreviewState == MaterialPreviewState.Normal)
            {
                return;
            }
            _rendererMaterialReplacerController.ResetMaterial();
            _materialPreviewState = MaterialPreviewState.Normal;
        }
    }
}
