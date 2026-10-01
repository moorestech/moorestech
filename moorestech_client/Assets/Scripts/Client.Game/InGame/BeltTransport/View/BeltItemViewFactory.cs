using System;
using Client.Game.InGame.Entity;
using Client.Game.InGame.Entity.Factory;
using Core.Master;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Client.Game.InGame.BeltTransport
{
    public sealed class BeltItemViewFactory : IBeltItemViewFactory
    {
        private readonly EntityObjectDatastore _parent;
        private readonly BeltConveyorItemEntityObjectFactory _factory = new();
        public BeltItemViewFactory(EntityObjectDatastore parent) { _parent = parent; }
        public UniTask<IEntityObject> CreateAsync(Guid identity, ItemId itemId, Vector3 position)
        {
            // シーン既存の親を共有し、スキット非表示を生成完了後にも継承する。
            // Share the scene parent so late creations inherit skit visibility.
            long entityId = BitConverter.ToInt64(identity.ToByteArray(), 0);
            return _factory.CreateItem(_parent.transform, entityId, itemId, position);
        }
    }
}
