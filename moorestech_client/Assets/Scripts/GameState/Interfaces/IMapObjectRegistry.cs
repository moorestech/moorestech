using System.Collections.Generic;
using UnityEngine;

namespace GameState
{
    public interface IMapObjectRegistry
    {
        IReadOnlyMapObject GetMapObject(int instanceId);
        IReadOnlyDictionary<int, IReadOnlyMapObject> AllMapObjects { get; }
    }

    public interface IReadOnlyMapObject
    {
        int InstanceId { get; }
        int MapObjectId { get; }
        Vector3 Position { get; }
        bool IsMined { get; }
    }
}