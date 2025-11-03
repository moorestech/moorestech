using System;
using Client.Common.Server;
using UnityEngine;

namespace Client.Game.InGame.Entity.Object
{
    /// <summary>
    /// 列車エンティティのビジュアル表示を担当
    /// サーバーから受信した位置情報をLinear補間してスムーズな移動を実現
    /// Responsible for visual display of train entities
    /// Achieves smooth movement by linear interpolation of position information received from server
    /// </summary>
    public class TrainEntityObject : MonoBehaviour, IEntityObject
    {
        public long EntityId { get; private set; }
        
        private float _linerTime;
        private Vector3 _previousPosition;
        private Vector3 _targetPosition;
        
        private Guid _trainId;
        private bool _isFacingForward = true;
        
        /// <summary>
        /// エンティティIDを設定し、初期化を行う
        /// Set entity ID and perform initialization
        /// </summary>
        public void Initialize(long entityId)
        {
            EntityId = entityId;
        }
        
        /// <summary>
        /// State文字列からTrainIdをパースする
        /// TrainIdはクライアント側でマスターデータ検索に使用される
        /// Parse TrainId from State string
        /// TrainId is used for master data lookup on client side
        /// </summary>
        public void SetTrainId(string stateString)
        {
            if (Guid.TryParse(stateString, out var trainId))
            {
                _trainId = trainId;
            }
            else
            {
                Debug.LogError($"[TrainEntityObject] Failed to parse TrainId from State: {stateString}");
                _trainId = Guid.Empty;
            }
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

