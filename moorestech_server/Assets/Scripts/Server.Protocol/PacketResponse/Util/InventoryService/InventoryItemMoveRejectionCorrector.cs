using System;
using Core.Inventory;
using MessagePack;
using Server.Event;
using Server.Event.EventReceive;
using Server.Event.EventReceive.UnifiedInventoryEvent;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse.Util.InventoryService
{
    /// <summary>
    ///     拒否された移動の両端を要求者へ現在値で送り直す。
    ///     クライアントは送信前に表示を先行更新し、サーバーは拒否時に何も書かず更新イベントも出ないため、ここが拒否時の唯一の訂正経路になる
    ///     Resends the current contents of both ends of a rejected move to the requester only, via the existing slot update events.
    ///     The client updates its display before sending and a rejection writes nothing (so no update event fires), making this the only correction path on rejection
    /// </summary>
    internal class InventoryItemMoveRejectionCorrector
    {
        private readonly EventProtocolProvider _eventProtocolProvider;

        internal InventoryItemMoveRejectionCorrector(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
        }

        // 未解決側はnullで来るので送らない
        // An unresolved side (e.g. a removed block) arrives as null; there is no real inventory to match, so nothing is sent for it
        internal void Resend(int requesterPlayerId, InventoryIdentifierMessagePack fromIdentifier, IOpenableInventory fromInventory, int fromSlot, InventoryIdentifierMessagePack toIdentifier, IOpenableInventory toInventory, int toSlot)
        {
            if (fromInventory != null) ResendSlot(fromIdentifier, fromInventory, fromSlot);
            if (toInventory != null) ResendSlot(toIdentifier, toInventory, toSlot);

            #region Internal

            void ResendSlot(InventoryIdentifierMessagePack identifier, IOpenableInventory inventory, int slot)
            {
                // 実在しないスロットはログだけ残す
                // A nonexistent slot has no display to correct, so log it and send nothing
                if (slot < 0 || inventory.GetSlotSize() <= slot)
                {
                    UnityEngine.Debug.LogWarning($"[InventoryItemMoveProtocol] Correction skipped for out-of-range slot: player={requesterPlayerId} inventory={identifier.InventoryType} slot={slot} slotSize={inventory.GetSlotSize()}");
                    return;
                }

                // 成功時と同じ更新イベントで現在値を送る
                // Send the current value through the same update event the client already subscribes to on success, per inventory type
                var item = inventory.GetItem(slot);
                switch (identifier.InventoryType)
                {
                    case InventoryType.Main:
                        AddEvent(MainInventoryUpdateEventPacket.EventTag, new MainInventoryUpdateEventMessagePack(slot, item));
                        break;
                    case InventoryType.Grab:
                        AddEvent(GrabInventoryUpdateEventPacket.EventTag, new GrabInventoryUpdateEventMessagePack(item));
                        break;
                    case InventoryType.Equipment:
                        AddEvent(EquipmentSlotUpdateEventPacket.EventTag, new EquipmentSlotUpdateEventMessagePack(slot, new ItemMessagePack(item)));
                        break;
                    case InventoryType.Block:
                    case InventoryType.Train:
                        AddEvent(UnifiedInventoryEventPacket.EventTag, UnifiedInventoryEventMessagePack.CreateUpdate(identifier, slot, item));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(identifier.InventoryType), identifier.InventoryType, "未対応のインベントリ種別");
                }
            }

            void AddEvent<T>(string tag, T messagePack)
            {
                _eventProtocolProvider.AddEvent(requesterPlayerId, tag, MessagePackSerializer.Serialize(messagePack));
            }

            #endregion
        }
    }
}
