using UnityEngine;

namespace Game.Entity.Interface
{
    public interface IEntity
    {
        EntityInstanceId InstanceId { get; }
        string EntityType { get; }
        
        Vector3 Position { get; }
        
        byte[] State { get; }
        
        void SetPosition(Vector3 serverVector3);
    }
}
