using Core.Item.Interface;
using Core.Master;
using UnityEngine;

namespace Game.Entity.Interface.EntityInstance
{
    public class ItemEntity : IEntity
    {
        public ItemEntity(EntityInstanceId instanceId, Vector3 position)
        {
            InstanceId = instanceId;
            Position = position;
        }
        
        public EntityInstanceId InstanceId { get; }
        public string EntityType => VanillaEntityType.VanillaItem;
        public Vector3 Position { get; private set; }
        public string State { get; private set; }
        
        
        public void SetPosition(Vector3 position)
        {
            Position = position;
        }
        
        public void SetState(IItemStack itemStack)
        {
            State = itemStack.Id + "," + itemStack.Count;
        }
        
        public void SetState(ItemId id, int count)
        {
            State = id + "," + count;
        }
    }
}