using System;
using Client.Game.InGame.Entity;
using Core.Master;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Client.Game.InGame.BeltTransport
{
    public interface IBeltItemViewFactory
    {
        UniTask<IEntityObject> CreateAsync(Guid identity, ItemId itemId, Vector3 position);
    }
}
