using System.Collections.Generic;
using Core.Item.Interface;
using UnityEngine;

namespace GameState
{
    public interface IPlayerState
    {
        Vector3 Position { get; }
        IReadOnlyList<IItemStack> MainInventory { get; }
        IItemStack GrabItem { get; }
    }
}