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
    public class TrainCarEntityObject : MonoBehaviour
    {
        public TrainCarInstanceId TrainCarInstanceId { get; private set; }
        public TrainCarMasterElement TrainCarMasterElement { get; set; }
        private const float ModelYawOffsetDegrees = -90f;
        /// <summary>
        /// モデル中心の前後オフセット
        /// Model forward center offset
        /// </summary>
        public float ModelForwardCenterOffset { get; private set; }
        private bool _debugAutoRun = false;//////////////////

        private RendererMaterialReplacerController _rendererMaterialReplacerController;
        private Rigidbody _poseRigidbody;
        private Vector3 _requestedPosition;
        private Quaternion _requestedRotation = Quaternion.identity;
        private bool _hasRequestedPose;
        private bool _hasAppliedInitialPose;

        /// <summary>
        /// 初期化を行う
        /// Perform initialization
        /// </summary>
        public void Initialize()
        {
            _debugAutoRun = DebugParameters.GetValueOrDefaultBool(DebugConst.TrainAutoRunKey);//////////////////
            _rendererMaterialReplacerController = new RendererMaterialReplacerController(gameObject);
            // 列車Colliderの移動を物理エンジンへ渡すためのRigidbodyを確保する
            // Ensure a Rigidbody so train collider movement is handled by physics
            EnsurePoseRigidbody();
            // モデル中心の前後オフセットをキャッシュする
            // Cache the model forward center offset
            ModelForwardCenterOffset = ResolveModelForwardCenterOffset();
            
            #region Internal
            float ResolveModelForwardCenterOffset()
            {
                // レンダラの境界中心から前後オフセットを算出する
                // Compute forward offset from renderer bounds center
                var renderers = GetComponentsInChildren<Renderer>(true);
                var combined = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++) combined.Encapsulate(renderers[i].bounds);
                var localForwardAxis = Quaternion.Euler(0f, -ModelYawOffsetDegrees, 0f) * Vector3.forward;
                var localCenter = transform.InverseTransformPoint(combined.center);
                return Vector3.Dot(localCenter, localForwardAxis);
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
            _requestedPosition = position;
            _requestedRotation = rotation;
            _hasRequestedPose = true;

            // 初回だけ物理補間せずに正しい位置へ配置する
            // Snap only the first pose so the train starts at the correct location
            if (_hasAppliedInitialPose)
            {
                return;
            }

            _poseRigidbody.position = position;
            _poseRigidbody.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            _hasAppliedInitialPose = true;
        }

        /// <summary>
        /// GameObject を破棄する
        /// Destroy GameObject
        /// </summary>
        public void Destroy()
        {
            Destroy(gameObject);
        }


        /// <summary>
        /// 毎フレーム呼ばれ
        /// Called every frame
        /// </summary>
        private void Update()
        {
            // デバッグ用：列車の自動運転（AutoRun）の ON/OFF が変化したらサーバへ通知する。
            // （監視は TrainCar 側で行い、変化があったタイミングでコマンド送信する）
            if (_debugAutoRun != DebugParameters.GetValueOrDefaultBool(DebugConst.TrainAutoRunKey))
            {
                _debugAutoRun = DebugParameters.GetValueOrDefaultBool(DebugConst.TrainAutoRunKey);
                OnTrainAutoRunChanged(_debugAutoRun);
                Debug.Log($"[Debug] Train auto run changed: {_debugAutoRun}");
            }
        }

        private void FixedUpdate()
        {
            // 姿勢要求が届くまで物理更新は行わない
            // Skip physics movement until a pose request is available
            if (!_hasRequestedPose)
            {
                return;
            }

            // kinematic Rigidbody経由でCollider移動を物理エンジンへ反映する
            // Apply collider movement through the kinematic Rigidbody
            _poseRigidbody.MovePosition(_requestedPosition);
            _poseRigidbody.MoveRotation(_requestedRotation);
        }

        private void EnsurePoseRigidbody()
        {
            // 既存Rigidbodyがあれば再利用し、無ければ追加する
            // Reuse an existing Rigidbody or add one if missing
            _poseRigidbody = GetComponent<Rigidbody>();
            if (_poseRigidbody == null)
            {
                _poseRigidbody = gameObject.AddComponent<Rigidbody>();
            }

            // 列車はサーバー同期姿勢で動くため、重力や力では動かさない
            // The train follows server-synced poses, not gravity or forces
            _poseRigidbody.isKinematic = true;
            _poseRigidbody.useGravity = false;
            _poseRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _poseRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        // 自動運転（AutoRun）の状態をサーバへ送信するローカル関数
        // Local function to send the auto-run state for all trains
        void OnTrainAutoRunChanged(bool isEnabled)
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
            var placePreviewMaterial = Resources.Load<Material>(MaterialConst.PreviewPlaceBlockMaterial);

            _rendererMaterialReplacerController.CopyAndSetMaterial(placePreviewMaterial);
            _rendererMaterialReplacerController.SetColor(MaterialConst.PreviewColorPropertyName, MaterialConst.NotPlaceableColor);
            Resources.UnloadAsset(placePreviewMaterial);
        }

        // 事実上、新規でTrainCarを設置しようとしたときに連結できますよを視覚的に知らせるための表示のみ用
        public void SetPlacementOverlapPreviewing()
        {
            // 設置候補重複ハイライトは設置可能色(青)で表示する
            // Show placement-overlap highlight in placeable color (blue)
            var placePreviewMaterial = Resources.Load<Material>(MaterialConst.PreviewPlaceBlockMaterial);
            _rendererMaterialReplacerController.CopyAndSetMaterial(placePreviewMaterial);
            _rendererMaterialReplacerController.SetColor(MaterialConst.PreviewColorPropertyName, MaterialConst.PlaceableColor);
            Resources.UnloadAsset(placePreviewMaterial);
        }

        public void ResetMaterial()
        {
            _rendererMaterialReplacerController.ResetMaterial();
        }
    }
}