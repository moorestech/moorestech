using Core.Item;
using UnityEngine;

namespace Game.Entity.Interface.EntityInstance
{
    public class ItemEntity : IEntity
    {
        public ItemEntity(long instanceId, Vector3 position)
        {
            InstanceId = instanceId;
            Position = position;
        }

        public long InstanceId { get; }
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

        public void SetState(int id, int count)
        {
            State = id + "," + count;
        }
    }
}