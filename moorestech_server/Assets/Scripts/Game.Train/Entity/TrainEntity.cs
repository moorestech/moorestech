using System;
using Game.Entity.Interface;
using Game.Train.Train;
using Game.Train.RailGraph;
using MessagePack;
using UnityEngine;

namespace Game.Train.Entity
{
    /// <summary>
    /// 列車（TrainUnit）をエンティティ同期システムで扱うためのアダプター
    /// あくまで仮なので、将来的には別のシステムで同期した方がいいかもしれない。
    /// Adapts TrainUnit to IEntity interface to enable client synchronization
    /// </summary>
    public class TrainEntity : IEntity
    {
        private readonly TrainUnit _trainUnit;
        public EntityInstanceId InstanceId { get; }
        
        public string EntityType => VanillaEntityType.VanillaTrain;

        /// <summary>
        /// TrainUnitからVector3座標を計算して返す
        /// Calculate train position by linear interpolation between two RailNodes
        /// </summary>
        public Vector3 Position
        {
            get
            {
                
            }
        }

        public TrainEntity(EntityInstanceId instanceId, TrainUnit trainUnit)
        {
            InstanceId = instanceId;
            _trainUnit = trainUnit;
        }

        /// <summary>
        /// 列車の位置はRailPositionで管理されるため、このメソッドは空実装
        /// Train position is managed by RailPosition, so this method is empty
        /// </summary>
        public void SetPosition(Vector3 serverVector3) { }
        
        public byte[] GetEntityData()
        {
            
            var state = new TrainEntityStateMessagePack(_trainUnit.TrainId);
            return MessagePackSerializer.Serialize(state);
        }}
    }
}
