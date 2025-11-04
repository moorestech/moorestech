using System;
using Core.Item.Interface;
using Core.Master;
using MessagePack;
using UnityEngine;

namespace Game.Entity.Interface.EntityInstance
{
    public class ItemEntity : IEntity
    {
        private byte[] _state;
        
        public ItemEntity(EntityInstanceId instanceId, Vector3 position)
        {
            InstanceId = instanceId;
            Position = position;
            _state = Array.Empty<byte>();
        }
        
        public byte[] State => _state;
        
        public EntityInstanceId InstanceId { get; }
        public string EntityType => VanillaEntityType.VanillaItem;
        public Vector3 Position { get; private set; }
        
        
        public void SetPosition(Vector3 position)
        {
            Position = position;
        }
        
        public void SetState(IItemStack itemStack)
        {
            // StateデータをMessagePack形式でシリアライズする
            // Serialize state data into MessagePack format
            var itemState = new ItemEntityStateMessagePack(itemStack.Id.AsPrimitive(), itemStack.Count);
            _state = MessagePackSerializer.Serialize(itemState);
        }
        
        public void SetState(ItemId id, int count)
        {
            // StateデータをMessagePack形式で保持する
            // Store state data in MessagePack format
            var itemState = new ItemEntityStateMessagePack(id.AsPrimitive(), count);
            _state = MessagePackSerializer.Serialize(itemState);
        }
    }
}
