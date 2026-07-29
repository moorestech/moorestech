using System;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface.Event;
using MessagePack;
using Server.Util.MessagePack;

namespace Server.Event.EventReceive
{
    /// <summary>
    ///     装備スロットの中身と選択中スロットの変化を、装備を持つプレイヤーへ伝えるパケット
    ///     Packet that reports equipment slot content and selected slot changes to the owning player
    /// </summary>
    public class EquipmentUpdateEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:equipmentUpdate";

        private readonly IEquipmentInventoryUpdateEvent _equipmentInventoryUpdateEvent;
        private readonly EventProtocolProvider _eventProtocolProvider;

        public EquipmentUpdateEventPacket(IEquipmentInventoryUpdateEvent equipmentInventoryUpdateEvent,
            EventProtocolProvider eventProtocolProvider)
        {
            _equipmentInventoryUpdateEvent = equipmentInventoryUpdateEvent;
            _eventProtocolProvider = eventProtocolProvider;
        }

        public void Load()
        {
            _equipmentInventoryUpdateEvent.Subscribe(ReceivedSlotEvent);
            _equipmentInventoryUpdateEvent.SubscribeSelectedEquipmentIndex(ReceivedSelectedIndexEvent);
        }

        private void ReceivedSlotEvent(PlayerInventoryUpdateEventProperties properties)
        {
            var messagePack = EquipmentUpdateEventMessagePack.CreateSlotEvent(properties.InventorySlot, properties.ItemStack);
            _eventProtocolProvider.AddEvent(properties.PlayerId, EventTag, MessagePackSerializer.Serialize(messagePack));
        }

        private void ReceivedSelectedIndexEvent(EquipmentSelectedIndexUpdateEventProperties properties)
        {
            var messagePack = EquipmentUpdateEventMessagePack.CreateSelectedEvent(properties.SelectedEquipmentIndex);
            _eventProtocolProvider.AddEvent(properties.PlayerId, EventTag, MessagePackSerializer.Serialize(messagePack));
        }
    }

    [MessagePackObject]
    public class EquipmentUpdateEventMessagePack
    {
        public const string SlotEventType = "slot";
        public const string SelectedEventType = "selected";

        // 選択変更イベントのSlotに入れる番兵。前例はUnifiedInventoryEventMessagePack.CreateRemove
        // Sentinel slot for selected events; the precedent is UnifiedInventoryEventMessagePack.CreateRemove
        public const int UnusedSlot = -1;

        // スロット変更イベントのSelectedIndexに入れる番兵。-1は素手として正当なのでintの最小値を使う
        // Sentinel selected index for slot events; -1 is a valid bare-hands value, so int.MinValue is used instead
        public const int UnusedSelectedIndex = int.MinValue;

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public EquipmentUpdateEventMessagePack()
        {
        }

        // EventTypeごとに必要フィールドが違うため、生成はstatic factoryへ寄せる
        // Fields differ per EventType, so construction goes through the static factories below
        private EquipmentUpdateEventMessagePack(string eventType, int slot, ItemMessagePack item, int selectedIndex)
        {
            EventType = eventType;
            Slot = slot;
            Item = item;
            SelectedIndex = selectedIndex;
        }

        // SelectedIndexは-1(素手)も正当なので、範囲外のUnusedSelectedIndexを入れてEventType分岐落ちを検知させる
        // A -1 selected index is valid (bare hands), so the out-of-range sentinel exposes a missed EventType branch
        public static EquipmentUpdateEventMessagePack CreateSlotEvent(int slot, IItemStack itemStack)
        {
            return new EquipmentUpdateEventMessagePack(SlotEventType, slot, new ItemMessagePack(itemStack), UnusedSelectedIndex);
        }

        // 選択変更でもItemは空アイテムで埋め、受信側がnull参照を踏まないようにする
        // Selected events still carry an empty item so receivers never dereference null
        // Slotは0が正当な装備スロットなので、受信側がEventType分岐を落としても気づけるよう範囲外の-1を入れる
        // Slot uses out-of-range -1 because 0 is a valid equipment slot, so a missed EventType branch fails loudly
        public static EquipmentUpdateEventMessagePack CreateSelectedEvent(int selectedIndex)
        {
            return new EquipmentUpdateEventMessagePack(SelectedEventType, UnusedSlot, new ItemMessagePack(ItemMaster.EmptyItemId, 0), selectedIndex);
        }

        [Key(0)] public string EventType { get; set; }

        [Key(1)] public int Slot { get; set; }

        [Key(2)] public ItemMessagePack Item { get; set; }

        [Key(3)] public int SelectedIndex { get; set; }
    }
}
