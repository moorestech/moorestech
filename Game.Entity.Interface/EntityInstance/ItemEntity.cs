using Core.Item;
using Game.Base;

namespace Game.Entity.Interface.EntityInstance
{
    public class ItemEntity : IEntity
    {
        public ItemEntity(long instanceId, ServerVector3 position)
        {
            InstanceId = instanceId;
            Position = position;
        }

        public long InstanceId { get; }
        public string EntityType => VanillaEntityType.VanillaItem;
        public ServerVector3 Position { get; private set; }
        public string State { get; private set; }

        public void SetState(IItemStack itemStack)
        {
            State = itemStack.Id + "," + itemStack.Count;
        }

        public void SetState(int id, int count)
        {
            State = id + "," + count;
        }
        
        
        public void SetPosition(ServerVector3 serverVector3)
        {
            Position = serverVector3;
        }
    }
}