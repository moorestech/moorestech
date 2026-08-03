using System;
using Game.Context;
using Game.PlayerInventory.Interface.Event;
using MessagePack;
using Server.Util.MessagePack;

namespace Server.Event.EventReceive
{
    /// <summary>
    ///     装備スロット更新を所有者へ通知するパケット
    ///     Packet reporting equipment slot updates to the owner
    /// </summary>
    public class EquipmentSlotUpdateEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:equipmentSlotUpdate";

        private readonly IEquipmentInventoryUpdateEvent _equipmentInventoryUpdateEvent;
        private readonly EventProtocolProvider _eventProtocolProvider;

        public EquipmentSlotUpdateEventPacket(IEquipmentInventoryUpdateEvent equipmentInventoryUpdateEvent,
            EventProtocolProvider eventProtocolProvider)
        {
            _equipmentInventoryUpdateEvent = equipmentInventoryUpdateEvent;
            _eventProtocolProvider = eventProtocolProvider;
        }

        public void Load()
        {
            _equipmentInventoryUpdateEvent.Subscribe(ReceivedSlotEvent);

            #region Internal

            void ReceivedSlotEvent(PlayerInventoryUpdateEventProperties properties)
            {
                var messagePack = new EquipmentSlotUpdateEventMessagePack(
                    properties.InventorySlot, new ItemMessagePack(properties.ItemStack));
                _eventProtocolProvider.AddEvent(properties.PlayerId, EventTag, MessagePackSerializer.Serialize(messagePack));
            }

            #endregion
        }
    }

    [MessagePackObject]
    public class EquipmentSlotUpdateEventMessagePack
    {
        [Key(0)] public int Slot { get; set; }
        [Key(1)] public ItemMessagePack Item { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public EquipmentSlotUpdateEventMessagePack()
        {
        }

        public EquipmentSlotUpdateEventMessagePack(int slot, ItemMessagePack item)
        {
            Slot = slot;
            Item = item;
        }
    }

}
