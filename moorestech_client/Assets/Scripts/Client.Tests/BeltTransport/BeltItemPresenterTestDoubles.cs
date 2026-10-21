using System.Collections.Generic;
using Client.Game.InGame.BeltTransport.View;
using Core.Master;
using UnityEngine;

namespace Client.Tests.BeltTransport
{
    // 表示物をその場で返し、個体IDごとに記録する。個体IDは生成順に1から振られている前提
    // Returns objects on the spot and records them by instance id; instance ids are assumed to be numbered from 1 in creation order
    internal sealed class FakeBeltItemObjectFactory : IBeltItemObjectFactory
    {
        public readonly Dictionary<long, FakeBeltItemObject> Created = new();
        public readonly Dictionary<long, ItemId> CreatedItemIds = new();

        public IBeltItemObject Create(ItemId itemId, Vector3 position)
        {
            var itemObject = new FakeBeltItemObject { Position = position };
            var instanceId = Created.Count + 1;
            Created.Add(instanceId, itemObject);
            CreatedItemIds.Add(instanceId, itemId);
            return itemObject;
        }
    }

    internal sealed class FakeBeltItemViewpoint : IBeltItemViewpoint
    {
        public Vector3 Position;
        public bool HasPosition = true;

        public FakeBeltItemViewpoint(Vector3 position)
        {
            Position = position;
        }

        public bool TryGetPosition(out Vector3 position)
        {
            position = Position;
            return HasPosition;
        }
    }

    internal sealed class FakeBeltItemObject : IBeltItemObject
    {
        public Vector3 Position;
        public bool IsDestroyed;

        public void SetPosition(Vector3 position)
        {
            Position = position;
        }

        public void Destroy()
        {
            IsDestroyed = true;
        }
    }
}
