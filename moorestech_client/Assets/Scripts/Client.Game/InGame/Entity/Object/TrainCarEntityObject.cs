using System;
using Client.Common.Server;
using Client.Game.InGame.Context;
using Common.Debug;
using Game.Entity.Interface;
using MessagePack;
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

        /// <summary>
        /// サーバーから同期されたRailPosition情報
        /// RailPosition information synchronized from server
        /// </summary>
        public RailPositionMessagePack RailPosition { get; private set; }

        /// <summary>
        /// 前方（進行方向側）に連結している車両のID。先頭車両の場合はGuid.Empty
        /// ID of the car connected in front (direction of travel). Guid.Empty if this is the front car
        /// </summary>
        public Guid PreviousCarId { get; private set; }

        /// <summary>
        /// 後方に連結している車両のID。最後尾車両の場合はGuid.Empty
        /// ID of the car connected behind. Guid.Empty if this is the rear car
        /// </summary>
        public Guid NextCarId { get; private set; }

        /// <summary>
        /// 先頭車両かどうか
        /// Whether this is the front car
        /// </summary>
        public bool IsFrontCar => PreviousCarId == Guid.Empty;

        /// <summary>
        /// 最後尾車両かどうか
        /// Whether this is the rear car
        /// </summary>
        public bool IsRearCar => NextCarId == Guid.Empty;

        private float _linerTime;
        private Vector3 _previousPosition;
        private Vector3 _targetPosition;

        private bool _isFacingForward = true;
        private bool _debugAutoRun = false;//////////////////

        /// <summary>
        /// エンティティIDを設定し、初期化を行う
        /// Set entity ID and perform initialization
        /// </summary>
        public void Initialize(long entityId)
        {
            EntityId = entityId;
            _debugAutoRun = DebugParameters.GetValueOrDefaultBool(DebugConst.TrainAutoRunKey);//////////////////
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
        /// エンティティデータを更新する
        /// Update entity data
        /// </summary>
        public void UpdateEntityData(byte[] entityData)
        {
            var state = MessagePackSerializer.Deserialize<TrainEntityStateMessagePack>(entityData);
            RailPosition = state.RailPosition;
            PreviousCarId = state.PreviousCarId;
            NextCarId = state.NextCarId;
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


            // デバッグ用で自動運転on off切り替え、この処理はtraincar単位で行われてしまっていることに注意。本来はtrainunit単位またはシーン単位だがどうせ消すのでこのままで！！
            if (_debugAutoRun != DebugParameters.GetValueOrDefaultBool(DebugConst.TrainAutoRunKey))
            {
                _debugAutoRun = DebugParameters.GetValueOrDefaultBool(DebugConst.TrainAutoRunKey);
                OnTrainAutoRunChanged(_debugAutoRun);
                Debug.Log($"[Debug] Train auto run changed: {_debugAutoRun}");
            }

            _linerTime += Time.deltaTime;
        }

        // 全列車の自動運転状態を送信するローカル関数
        // Local function to send the auto-run state for all trains
        void OnTrainAutoRunChanged(bool isEnabled)
        {
            // サーバーへ全列車の自動運転切り替えコマンドを送信
            // Send the auto-run toggle command for all trains to the server
            var command = isEnabled
                ? $"{SendCommandProtocol.TrainAutoRunCommand} {SendCommandProtocol.TrainAutoRunOnArgument}"
                : $"{SendCommandProtocol.TrainAutoRunCommand} {SendCommandProtocol.TrainAutoRunOffArgument}";
            ClientContext.VanillaApi.SendOnly.SendCommand(command);
        }

    }
}

