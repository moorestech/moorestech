using System;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train;
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
        private bool _isFacingForward = true;
        private bool _debugAutoRun = false;

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
            // モデル中心の前後オフセットをキャッシュする
            // Cache the model forward center offset
            ModelForwardCenterOffset = ResolveModelForwardCenterOffset();
        }
        
        public void SetTrain(Guid trainCarId, TrainCarMasterElement trainCarMasterElement, TrainUnitClientCache trainUnitClientCache)
        {
            TrainCarId = trainCarId;
            TrainCarMasterElement = trainCarMasterElement;
            var units = trainUnitClientCache.Units;
            // TrainUnitからObjectにアクセスするため
            // Access from TrainUnit to Object
            foreach (var unit in units)
            {
                if (TrainCarId == unit.Key)
                {
                    unit.Value.RegisterTrainCarEntityObjects(this);
                    break;
                }
            }
        }
        
        //IEntityObject実装のためだけ
        public void SetDirectPosition(Vector3 position)
        {
        }
        //IEntityObject実装のためだけ
        public void SetEntityData(byte[] entityEntityData)
        {
        }
        //IEntityObject実装のためだけ
        public void SetPositionWithLerp(Vector3 position)
        {
        }

        /// <summary>
        /// 即座に位置と角度を設定する（補間なし）
        /// Set position and rotation immediately (without interpolation)
        /// </summary>
        public void SetDirectPose(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>
        /// GameObject を破棄する
        /// Destroy GameObject
        /// </summary>
        public void Destroy()
        {
            Destroy(gameObject);
        }

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
    }
}
