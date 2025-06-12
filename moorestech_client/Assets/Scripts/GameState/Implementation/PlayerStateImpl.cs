using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using UnityEngine;

namespace GameState.Implementation
{
    public class PlayerStateImpl : IPlayerState
    {
        private readonly List<IItemStack> _mainInventory = new();
        private IItemStack _grabItem;
        private Vector3 _position;

        public PlayerStateImpl()
        {
            // Initialize with empty inventory
            var itemStackFactory = ServerContext.ItemStackFactory;
            for (var i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                _mainInventory.Add(itemStackFactory.CreatEmpty());
            }
            _grabItem = itemStackFactory.CreatEmpty();
        }

        public Vector3 Position => _position;

        public IReadOnlyList<IItemStack> MainInventory => _mainInventory;

        public IItemStack GrabItem => _grabItem;

        public void UpdatePosition(Vector3 position)
        {
            _position = position;
        }

        public void UpdateMainInventory(List<IItemStack> inventory)
        {
            _mainInventory.Clear();
            _mainInventory.AddRange(inventory);
        }

        public void UpdateGrabItem(IItemStack item)
        {
            _grabItem = item;
        }
    }
}