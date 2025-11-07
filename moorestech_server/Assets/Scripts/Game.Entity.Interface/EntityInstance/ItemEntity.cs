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
        
        
        public ItemEntity(EntityInstanceId instanceId, Vector3 position)
        {
            InstanceId = instanceId;
            Position = position;
        }
        
        public void SetItemData(ItemId id, int count)
        {
            _id = id;
            _count = count;
        }
        
        public void SetPosition(Vector3 position)
        {
            Position = position;
        }
        public byte[] GetEntityData()
        {
            var itemState = new ItemEntityStateMessagePack(_id, _count);
            return MessagePackSerializer.Serialize(itemState);
        }
    }
}
