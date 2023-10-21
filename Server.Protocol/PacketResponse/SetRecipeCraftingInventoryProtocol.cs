using System;
using System.Collections.Generic;
using Core.Item;
using Core.Item.Config;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util.RecipePlace;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    public class SetRecipeCraftingInventoryProtocol : IPacketResponse
    {
        public const string Tag = "va:setRecipeCraftingInventory";
        private readonly IItemConfig _itemConfig;
        private readonly ItemStackFactory _itemStackFactory;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public SetRecipeCraftingInventoryProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            _itemConfig = serviceProvider.GetService<IItemConfig>();
        }

        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<SetRecipeCraftingInventoryProtocolMessagePack>(payload.ToArray());

            var mainInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;
            var craftingInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId).CraftingOpenableInventory;
            var grabInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId).GrabInventory;


            
            MovingItemToMainInventory.Move(mainInventory, craftingInventory, grabInventory);

            
            //todo isPlaceable 
            var (isPlaceable, mainInventoryRequiredItemCount) = CheckPlaceableRecipe.IsPlaceable(mainInventory, data.Recipe);

            
            var moveItem = CalcCraftInventoryPlaceItem.Calc(_itemStackFactory, _itemConfig, data.Recipe, mainInventoryRequiredItemCount);

            
            MoveRecipeMainInventoryToCraftInventory.Move(_itemStackFactory, mainInventory, craftingInventory, moveItem);

            return new List<List<byte>>();
        }
    }

    [MessagePackObject(true)]
    public class SetRecipeCraftingInventoryProtocolMessagePack : ProtocolMessagePackBase
    {
        public SetRecipeCraftingInventoryProtocolMessagePack(int playerId, ItemMessagePack[] recipe)
        {
            Tag = SetRecipeCraftingInventoryProtocol.Tag;
            PlayerId = playerId;
            Recipe = recipe;
        }

        [Obsolete("。。")]
        public SetRecipeCraftingInventoryProtocolMessagePack()
        {
        }

        public ItemMessagePack[] Recipe { get; set; }
        public int PlayerId { get; set; }
    }
}