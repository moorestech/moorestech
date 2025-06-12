using System.Collections.Generic;
using UnityEngine;

namespace GameState
{
    public interface IEntityRegistry
    {
        IReadOnlyList<IClientEntity> GetEntities();
        IClientEntity GetEntity(long instanceId);
    }

    public interface IClientEntity
    {
        long InstanceId { get; }
        string EntityType { get; }
        Vector3 Position { get; }
        string State { get; }
    }
}