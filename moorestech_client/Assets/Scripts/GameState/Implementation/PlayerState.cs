using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Network.API;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using UnityEngine;

namespace GameState.Implementation
{
    public class PlayerState : IPlayerState, IVanillaApiConnectable
    {
        private readonly List<IItemStack> _mainInventory = new();
        private IItemStack _grabItem;
        private Vector3 _position;
        private readonly IItemStackFactory _itemStackFactory;

        public PlayerState()
        {
            // Initialize with empty inventory
            _itemStackFactory = ServerContext.ItemStackFactory;
            for (var i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                _mainInventory.Add(_itemStackFactory.CreatEmpty());
            }
            _grabItem = _itemStackFactory.CreatEmpty();
        }
        
        public void ConnectToVanillaApi(InitialHandshakeResponse initialHandshakeResponse)
        {
            
            // Initialize from handshake response
            var playerInventory = initialHandshakeResponse.Inventory;
            UpdateMainInventory(playerInventory.MainInventory);
            UpdateGrabItem(playerInventory.GrabItem);
            
            // Subscribe to inventory events
            SubscribeToInventoryEvents();
        }
        
        private void SubscribeToInventoryEvents()
        {
            // Main inventory update event
            ClientContext.VanillaApi.Event.SubscribeEventResponse(MainInventoryUpdateEventPacket.EventTag, payload =>
            {
                var data = MessagePackSerializer.Deserialize<MainInventoryUpdateEventMessagePack>(payload);
                UpdateMainInventorySlot(data.Slot, data.Item);
            });
            
            // Grab inventory update event
            ClientContext.VanillaApi.Event.SubscribeEventResponse(GrabInventoryUpdateEventPacket.EventTag, payload =>
            {
                var data = MessagePackSerializer.Deserialize<GrabInventoryUpdateEventMessagePack>(payload);
                var grabItem = _itemStackFactory.Create(data.Item.Id, data.Item.Count);
                UpdateGrabItem(grabItem);
            });
        }
        
        private void UpdateMainInventorySlot(int slot, ItemMessagePack itemStack)
        {
            if (slot >= 0 && slot < _mainInventory.Count)
            {
                _mainInventory[slot] = _itemStackFactory.Create(itemStack.Id, itemStack.Count);
            }
        }

        public Vector3 Position => _position;

        public IReadOnlyList<IItemStack> MainInventory => _mainInventory;

        public IItemStack GrabItem => _grabItem;

        public void UpdatePosition(Vector3 position)
        {
            _position = position;
        }

        public void UpdateMainInventory(IReadOnlyList<IItemStack> inventory)
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