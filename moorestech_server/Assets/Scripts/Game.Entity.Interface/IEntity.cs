using UnityEngine;

namespace Game.Entity.Interface
{
    public interface IEntity
    {
        long InstanceId { get; }
        string EntityType { get; }

        Vector3 Position { get; }

        string State { get; }

        void SetPosition(Vector3 serverVector3);
    }
}