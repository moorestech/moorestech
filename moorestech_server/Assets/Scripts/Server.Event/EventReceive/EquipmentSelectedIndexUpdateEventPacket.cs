using System;
using Game.Context;
using Game.PlayerInventory.Interface.Event;
using MessagePack;

namespace Server.Event.EventReceive
{
    /// <summary>
    ///     装備の選択位置を、装備を持つプレイヤーへ伝えるパケット
    ///     Packet reporting the selected equipment index to the owning player
    /// </summary>
    public class EquipmentSelectedIndexUpdateEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:equipmentSelectedIndexUpdate";

        private readonly IEquipmentInventoryUpdateEvent _equipmentInventoryUpdateEvent;
        private readonly EventProtocolProvider _eventProtocolProvider;

        public EquipmentSelectedIndexUpdateEventPacket(
            IEquipmentInventoryUpdateEvent equipmentInventoryUpdateEvent,
            EventProtocolProvider eventProtocolProvider)
        {
            _equipmentInventoryUpdateEvent = equipmentInventoryUpdateEvent;
            _eventProtocolProvider = eventProtocolProvider;
        }

        public void Load()
        {
            _equipmentInventoryUpdateEvent.SubscribeSelectedEquipmentIndex(ReceivedSelectedIndexEvent);

            #region Internal

            void ReceivedSelectedIndexEvent(EquipmentSelectedIndexUpdateEventProperties properties)
            {
                var messagePack = new EquipmentSelectedIndexUpdateEventMessagePack(properties.SelectedEquipmentIndex);
                _eventProtocolProvider.AddEvent(properties.PlayerId, EventTag, MessagePackSerializer.Serialize(messagePack));
            }

            #endregion
        }
    }

    [MessagePackObject]
    public class EquipmentSelectedIndexUpdateEventMessagePack
    {
        [Key(0)] public int SelectedIndex { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public EquipmentSelectedIndexUpdateEventMessagePack()
        {
        }

        public EquipmentSelectedIndexUpdateEventMessagePack(int selectedIndex)
        {
            SelectedIndex = selectedIndex;
        }
    }
}
