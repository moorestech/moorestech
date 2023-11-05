using System;
using System.Linq;
using Core.Item;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using MessagePack;
using Server.Util.MessagePack;

namespace Server.Event.EventReceive
{
    public class CraftingInventoryUpdateToSetEventPacket
    {
        public const string EventTag = "va:event:craftInvUpdate";
        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public CraftingInventoryUpdateToSetEventPacket(EventProtocolProvider eventProtocolProvider,
            ICraftInventoryUpdateEvent craftInventoryUpdateEvent, IPlayerInventoryDataStore playerInventoryDataStore)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _playerInventoryDataStore = playerInventoryDataStore;
            craftInventoryUpdateEvent.Subscribe(ReceivedEvent);
        }


        private void ReceivedEvent(PlayerInventoryUpdateEventProperties properties)
        {
            var craftInventory = _playerInventoryDataStore.GetInventoryData(properties.PlayerId);

            var creatableItem = craftInventory.CraftingOpenableInventory.GetCreatableItem();
            var isCreatable = craftInventory.CraftingOpenableInventory.IsCreatable();


            var payload = MessagePackSerializer.Serialize(new CraftingInventoryUpdateEventMessagePack(
                properties.InventorySlot, properties.ItemStack, isCreatable, creatableItem
            )).ToList();

            _eventProtocolProvider.AddEvent(properties.PlayerId, payload);
        }
    }


    [MessagePackObject(true)]
    public class CraftingInventoryUpdateEventMessagePack : EventProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public CraftingInventoryUpdateEventMessagePack()
        {
        }

        public CraftingInventoryUpdateEventMessagePack(int slot, IItemStack itemStack, bool isCreatable,
            IItemStack creatableItem)
        {
            EventTag = CraftingInventoryUpdateToSetEventPacket.EventTag;
            Slot = slot;
            Item = new ItemMessagePack(itemStack);
            IsCraftable = isCreatable;
            CreatableItem = new ItemMessagePack(creatableItem);
        }

        public ItemMessagePack CreatableItem { get; set; }

        public bool IsCraftable { get; set; }

        public ItemMessagePack Item { get; set; }

        public int Slot { get; set; }
    }
}