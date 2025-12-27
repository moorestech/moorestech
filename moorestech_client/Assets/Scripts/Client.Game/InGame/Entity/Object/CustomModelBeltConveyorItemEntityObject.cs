using Client.Common.Server;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor.BeltConveyor;
using Client.Game.InGame.Context;
using Client.Game.InGame.Entity.Object.Util;
using Game.Entity.Interface;
using UnityEngine;

namespace Client.Game.InGame.Entity.Object
{
    /// <summary>
    /// カスタム3Dモデルを使用するアイテムエンティティ
    /// Item entity using custom 3D model
    /// </summary>
    public class CustomModelBeltConveyorItemEntityObject : MonoBehaviour, IEntityObject
    {
        public long EntityId { get; private set; }

        private float _linerTime;
        private Vector3 _previousPosition;
        private Vector3 _targetPosition;

        private void Awake()
        {
            _targetPosition = transform.position;
            _previousPosition = transform.position;
        }

        public void Initialize(long entityId)
        {
            EntityId = entityId;
        }

        private void Update()
        {
            // 補間処理
            // Interpolation processing
            var rate = _linerTime / NetworkConst.UpdateIntervalSeconds;
            rate = Mathf.Clamp01(rate);
            transform.position = Vector3.Lerp(_previousPosition, _targetPosition, rate);
            _linerTime += Time.deltaTime;
        }

        public void SetDirectPosition(Vector3 position)
        {
            _targetPosition = position;
            _previousPosition = position;
            transform.position = position;
        }

        public void SetPositionWithLerp(Vector3 position)
        {
            _previousPosition = transform.position;
            _targetPosition = position;
            _linerTime = 0;
        }
        public void SetEntityData(byte[] entityEntityData)
        {
            var pos = BeltConveyorItemPositionCalculator.CalculatePosition(entityEntityData);
            SetPositionWithLerp(pos);
        }

        public void Destroy()
        {
            Destroy(gameObject);
        }
    }
}
