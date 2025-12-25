using System;
using Core.Item.Interface;
using Core.Master;
using MessagePack;
using UnityEngine;

namespace Game.Entity.Interface.EntityInstance
{
    public class BeltConveyorItemEntity : IEntity
    {
        public EntityInstanceId InstanceId { get; }
        public string EntityType => VanillaEntityType.VanillaItem;
        public Vector3 Position { get; private set; }
        
        private ItemId _id;
        private int _count;
        private Guid? _sourceConnectorGuid;
        private Guid? _goalConnectorGuid;


        public BeltConveyorItemEntity(EntityInstanceId instanceId, Vector3 position)
        {
            InstanceId = instanceId;
            Position = position;
        }

        public void SetItemData(ItemId id, int count, Guid? sourceConnectorGuid, Guid? goalConnectorGuid)
        {
            _id = id;
            _count = count;
            _sourceConnectorGuid = sourceConnectorGuid;
            _goalConnectorGuid = goalConnectorGuid;
        }

        public void SetPosition(Vector3 position)
        {
            Position = position;
        }
        public byte[] GetEntityData()
        {
            var itemState = new BeltConveyorItemEntityStateMessagePack(_id, _count, _sourceConnectorGuid, _goalConnectorGuid);
            return MessagePackSerializer.Serialize(itemState);
        }
    }
}
