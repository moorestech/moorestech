using System;
using Client.Common.Server;
using Mooresmaster.Model.TrainModule;
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
        
        private bool _isFacingForward = true;
        
        /// <summary>
        /// エンティティIDを設定し、初期化を行う
        /// Set entity ID and perform initialization
        /// </summary>
        public void Initialize(long entityId)
        {
            EntityId = entityId;
        }
        
        public void SetTrain(Guid trainCarId, TrainCarMasterElement trainCarMasterElement)
        {
            TrainCarId = trainCarId;
            TrainCarMasterElement = trainCarMasterElement;
        }
        
        /// <summary>
        /// 即座に位置を設定する（補間なし）
        /// Set position immediately (without interpolation)
        /// </summary>
        public void SetDirectPosition(Vector3 position)
        {
            _targetPosition = position;
            _previousPosition = position;
            transform.position = position;
            _linerTime = 0;
        }
        
        /// <summary>
        /// 補間を開始して新しい位置に移動する
        /// Start interpolation to move to new position
        /// </summary>
        public void SetPositionWithLerp(Vector3 position)
        {
            _previousPosition = transform.position;
            _targetPosition = position;
            _linerTime = 0;
        }
        
        /// <summary>
        /// GameObjectを破棄する
        /// Destroy GameObject
        /// </summary>
        public void Destroy()
        {
            Destroy(gameObject);
        }
        
        /// <summary>
        /// 毎フレーム呼ばれ、Linear補間で位置を更新する
        /// Called every frame, updates position with linear interpolation
        /// </summary>
        private void Update()
        {
            // NetworkConst.UpdateIntervalSeconds秒かけて補間
            // Interpolate over NetworkConst.UpdateIntervalSeconds seconds
            var rate = _linerTime / NetworkConst.UpdateIntervalSeconds;
            rate = Mathf.Clamp01(rate);
            transform.position = Vector3.Lerp(_previousPosition, _targetPosition, rate);
            _linerTime += Time.deltaTime;
        }
    }
}

