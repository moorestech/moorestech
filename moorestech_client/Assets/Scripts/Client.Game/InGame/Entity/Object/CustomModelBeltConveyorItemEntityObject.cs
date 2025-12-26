using Client.Common.Server;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor.BeltConveyor;
using Client.Game.InGame.Context;
using Game.Entity.Interface;
using UnityEngine;

namespace Client.Game.InGame.Entity.Object
{
    /// <summary>
    /// カスタム3Dモデルを使用するアイテムエンティティ
    /// Item entity using custom 3D model
    /// </summary>
    public class CustomModelBeltConveyorItemEntityObject : MonoBehaviour, IEntityObject, IBeltConveyorItemEntityObject
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
        
        public void SetBeltConveyorItemPosition(BeltConveyorItemEntityStateMessagePack state, bool useLerp)
        {
            // ベルトパスから目標座標を算出して反映する
            // Apply target position resolved from belt path
            if (!TryGetTargetPosition(state, out var targetPosition)) return;
            if (useLerp) SetPositionWithLerp(targetPosition);
            else SetDirectPosition(targetPosition);

            #region Internal

            bool TryGetTargetPosition(BeltConveyorItemEntityStateMessagePack entityState, out Vector3 target)
            {
                target = default;
                
                var blockPosition = new Vector3Int(entityState.BlockPosX, entityState.BlockPosY, entityState.BlockPosZ);
                if (!ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(blockPosition, out BlockGameObject block)) return false;
                var path = block.GetComponentInChildren<BeltConveyorItemPath>();
                if (path == null) return false;
                
                var startId = entityState.SourceConnectorGuid?.ToString();
                var goalId = entityState.GoalConnectorGuid?.ToString();
                target = path.GetWorldPosition(startId, goalId, entityState.RemainingPercent);
                return true;
            }

            #endregion
        }

        public void Destroy()
        {
            Destroy(gameObject);
        }
    }
}
