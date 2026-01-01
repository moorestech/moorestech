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
        
        private float _linerTime;
        private Vector3 _previousPosition;
        private Vector3 _targetPosition;
        private Quaternion _previousRotation;
        private Quaternion _targetRotation;
        
        private bool _isFacingForward = true;
        private bool _debugAutoRun = false;//////////////////
        
        private RendererMaterialReplacerController _rendererMaterialReplacerController;

        /// <summary>
        /// 繧ｨ繝ｳ繝・ぅ繝・ぅID繧定ｨｭ螳壹＠縲∝・譛溷喧繧定｡後≧
        /// Set entity ID and perform initialization
        /// </summary>
        public void Initialize(long entityId)
        {
            EntityId = entityId;
            _debugAutoRun = DebugParameters.GetValueOrDefaultBool(DebugConst.TrainAutoRunKey);//////////////////
            
            _rendererMaterialReplacerController = new RendererMaterialReplacerController(gameObject);
            _previousRotation = transform.rotation;
            _targetRotation = transform.rotation;
        }
        
        public void SetTrain(Guid trainCarId, TrainCarMasterElement trainCarMasterElement)
        {
            TrainCarId = trainCarId;
            TrainCarMasterElement = trainCarMasterElement;
        }
        
        /// <summary>
        /// 蜊ｳ蠎ｧ縺ｫ菴咲ｽｮ繧定ｨｭ螳壹☆繧具ｼ郁｣憺俣縺ｪ縺暦ｼ・
        /// Set position immediately (without interpolation)
        /// </summary>
        public void SetDirectPosition(Vector3 position)
        {
            SetDirectPose(position, transform.rotation);
        }
        
        /// <summary>
        /// 陬憺俣繧帝幕蟋九＠縺ｦ譁ｰ縺励＞菴咲ｽｮ縺ｫ遘ｻ蜍輔☆繧・
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
        /// 陬憺俣繧帝幕蟋九＠縺ｦ譁ｰ縺励＞菴咲ｽｮ縺ｨ隗貞ｺｦ縺ｫ遘ｻ蜍輔☆繧・
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
        /// GameObject繧堤ｴ譽・☆繧・
        /// Destroy GameObject
        /// </summary>
        public void Destroy()
        {
            Destroy(gameObject);
        }
        
        /// <summary>
        /// 豈弱ヵ繝ｬ繝ｼ繝蜻ｼ縺ｰ繧後´inear陬憺俣縺ｧ菴咲ｽｮ繧呈峩譁ｰ縺吶ｋ
        /// Called every frame, updates position with linear interpolation
        /// </summary>
        private void Update()
        {
            // NetworkConst.UpdateIntervalSeconds遘偵°縺代※陬憺俣
            // Interpolate over NetworkConst.UpdateIntervalSeconds seconds
            var rate = _linerTime / NetworkConst.UpdateIntervalSeconds;
            rate = Mathf.Clamp01(rate);
            transform.position = Vector3.Lerp(_previousPosition, _targetPosition, rate);
            transform.rotation = Quaternion.Slerp(_previousRotation, _targetRotation, rate);


            // 繝・ヰ繝・げ逕ｨ縺ｧ閾ｪ蜍暮°霆｢on off蛻・ｊ譖ｿ縺医√％縺ｮ蜃ｦ逅・・traincar蜊倅ｽ阪〒陦後ｏ繧後※縺励∪縺｣縺ｦ縺・ｋ縺薙→縺ｫ豕ｨ諢上よ悽譚･縺ｯtrainunit蜊倅ｽ阪∪縺溘・繧ｷ繝ｼ繝ｳ蜊倅ｽ阪□縺後←縺・○豸医☆縺ｮ縺ｧ縺薙・縺ｾ縺ｾ縺ｧ・・ｼ・
            if (_debugAutoRun != DebugParameters.GetValueOrDefaultBool(DebugConst.TrainAutoRunKey))
            {
                _debugAutoRun = DebugParameters.GetValueOrDefaultBool(DebugConst.TrainAutoRunKey);
                OnTrainAutoRunChanged(_debugAutoRun);
                Debug.Log($"[Debug] Train auto run changed: {_debugAutoRun}");
            }

            _linerTime += Time.deltaTime;
        }

        // 蜈ｨ蛻苓ｻ翫・閾ｪ蜍暮°霆｢迥ｶ諷九ｒ騾∽ｿ｡縺吶ｋ繝ｭ繝ｼ繧ｫ繝ｫ髢｢謨ｰ
        // Local function to send the auto-run state for all trains
        void OnTrainAutoRunChanged(bool isEnabled)
        {
            // 繧ｵ繝ｼ繝舌・縺ｸ蜈ｨ蛻苓ｻ翫・閾ｪ蜍暮°霆｢蛻・ｊ譖ｿ縺医さ繝槭Φ繝峨ｒ騾∽ｿ｡
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
    }
}

