using System;
using Core.Item.Interface;
using Core.Master;
using MessagePack;
using UnityEngine;

namespace Game.Entity.Interface.EntityInstance
{
    public class ItemEntity : IEntity
    {
        public EntityInstanceId InstanceId { get; }
        public string EntityType => VanillaEntityType.VanillaItem;
        public Vector3 Position { get; private set; }
        
        private ItemId _id;
        private int _count;
        private string _sourcePathId;
        private string _goalPathId;


        public ItemEntity(EntityInstanceId instanceId, Vector3 position)
        {
            InstanceId = instanceId;
            Position = position;
        }

        public void SetItemData(ItemId id, int count, string sourcePathId, string goalPathId)
        {
            _id = id;
            _count = count;
            _sourcePathId = sourcePathId;
            _goalPathId = goalPathId;
        }

        public void SetPosition(Vector3 position)
        {
            Position = position;
        }
        public byte[] GetEntityData()
        {
            var itemState = new ItemEntityStateMessagePack(_id, _count, _sourcePathId, _goalPathId);
            return MessagePackSerializer.Serialize(itemState);
        }
    }
}
