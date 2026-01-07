using System;
using Client.Common;
using Client.Common.Server;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Common.Debug;
using Mooresmaster.Model.TrainModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.Entity.Object
{
    public class TrainCarEntityObject : MonoBehaviour, IEntityObject
    {
        public long EntityId { get; private set; }
        public Guid TrainCarId { get; private set; }
        public TrainCarMasterElement TrainCarMasterElement { get; set; }
        public bool DestroyFlagIfNoUpdate => false;
        /// <summary>
        /// モデル中心の前後オフセット
        /// Model forward center offset
        /// </summary>
        public float ModelForwardCenterOffset { get; private set; }

        private float _linerTime;
        private Vector3 _previousPosition;
        private Vector3 _targetPosition;
        private Quaternion _previousRotation;
        private Quaternion _targetRotation;

        private bool _isFacingForward = true;
        private bool _debugAutoRun = false;//////////////////

        private RendererMaterialReplacerController _rendererMaterialReplacerController;

        /// <summary>
        /// エンティティIDを設定し、初期化を行う
        /// Set entity ID and perform initialization
        /// </summary>
        public void Initialize(long entityId)
        {
            EntityId = entityId;
            _debugAutoRun = DebugParameters.GetValueOrDefaultBool(DebugConst.TrainAutoRunKey);//////////////////

            _rendererMaterialReplacerController = new RendererMaterialReplacerController(gameObject);
            _previousRotation = transform.rotation;
            _targetRotation = transform.rotation;

            // モデル中心の前後オフセットをキャッシュする
            // Cache the model forward center offset
            ModelForwardCenterOffset = ResolveModelForwardCenterOffset();
        }

        public void SetTrain(Guid trainCarId, TrainCarMasterElement trainCarMasterElement)
        {
            TrainCarId = trainCarId;
            TrainCarMasterElement = trainCarMasterElement;
        }

        /// <summary>
        /// 位置を即座に設定する（補間なし）
        /// Set position immediately (without interpolation)
        /// </summary>
        public void SetDirectPosition(Vector3 position)
        {
            SetDirectPose(position, transform.rotation);
        }

        /// <summary>
        /// 補間を開始し、新しい位置へ移動する
        /// Start interpolation to move to new position
        /// </summary>
        public void SetPositionWithLerp(Vector3 position)
        {
            SetPoseWithLerp(position, transform.rotation);
        }

        /// <summary>
        /// 即座に位置と角度を設定する（補間なし）
        /// Set position and rotation immediately (without interpolation)
        /// </summary>
        public void SetDirectPose(Vector3 position, Quaternion rotation)
        {
            _targetPosition = position;
            _previousPosition = position;
            _targetRotation = rotation;
            _previousRotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            _linerTime = 0;
        }

        /// <summary>
        /// 補間を開始し、新しい姿勢（位置＋角度）へ移動する
        /// Start interpolation to move to new pose
        /// </summary>
        public void SetPoseWithLerp(Vector3 position, Quaternion rotation)
        {
            _previousPosition = transform.position;
            _targetPosition = position;
            _previousRotation = transform.rotation;
            _targetRotation = rotation;
            _linerTime = 0;
        }

        /// <summary>
        /// GameObject を破棄する
        /// Destroy GameObject
        /// </summary>
        public void Destroy()
        {
            Destroy(gameObject);
        }

        public void SetEntityData(byte[] entityEntityData)
        {

        }

        /// <summary>
        /// 毎フレーム呼ばれ、linear 補間で位置と角度を更新する
        /// Called every frame, updates position with linear interpolation
        /// </summary>
        private void Update()
        {
            // NetworkConst.UpdateIntervalSeconds 秒かけて補間する
            // Interpolate over NetworkConst.UpdateIntervalSeconds seconds
            var rate = _linerTime / NetworkConst.UpdateIntervalSeconds;
            rate = Mathf.Clamp01(rate);
            transform.position = Vector3.Lerp(_previousPosition, _targetPosition, rate);
            transform.rotation = Quaternion.Slerp(_previousRotation, _targetRotation, rate);

            // デバッグ用：列車の自動運転（AutoRun）の ON/OFF が変化したらサーバへ通知する。
            // （監視は TrainCar 側で行い、変化があったタイミングでコマンド送信する）
            if (_debugAutoRun != DebugParameters.GetValueOrDefaultBool(DebugConst.TrainAutoRunKey))
            {
                _debugAutoRun = DebugParameters.GetValueOrDefaultBool(DebugConst.TrainAutoRunKey);
                OnTrainAutoRunChanged(_debugAutoRun);
                Debug.Log($"[Debug] Train auto run changed: {_debugAutoRun}");
            }

            _linerTime += Time.deltaTime;
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

        public void ResetMaterial()
        {
            _rendererMaterialReplacerController.ResetMaterial();
        }

        #region Internal

        private float ResolveModelForwardCenterOffset()
        {
            // レンダラの境界中心から前後オフセットを算出する
            // Compute forward offset from renderer bounds center
            var renderers = GetComponentsInChildren<Renderer>(true);
            var combined = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) combined.Encapsulate(renderers[i].bounds);
            var localCenter = transform.InverseTransformPoint(combined.center);
            return localCenter.z;
        }

        #endregion
    }
}
