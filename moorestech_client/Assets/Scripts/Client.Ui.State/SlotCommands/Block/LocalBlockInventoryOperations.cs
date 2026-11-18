using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Items;
using Client.Ui.State.ItemMove;
using UnityEngine;

namespace Client.Ui.State.SlotCommands.Block
{
    /// <summary>
    /// 本番のブロック側の口。サブインベントリは結合インベントリの MainSlotCount 以降に並ぶ（ホストの BlockAreaSlotParser と同じ番地）
    /// Production block-side port; the sub-inventory follows MainSlotCount in the combined inventory (same addressing as the host BlockAreaSlotParser)
    /// </summary>
    public sealed class LocalBlockInventoryOperations : IBlockInventoryOperations
    {
        private readonly LocalPlayerInventoryController _controller;
        private readonly BlockSlotReader _reader;

        public LocalBlockInventoryOperations(LocalPlayerInventoryController controller, BlockSlotReader reader)
        {
            _controller = controller;
            _reader = reader;
        }

        public bool TryReadBlockSlots(out IReadOnlyList<SlotItem> slots)
        {
            return _reader.TryRead(out slots);
        }

        public PlayerItemMoveOutcome Move(ItemMoveSlotRef from, ItemMoveSlotRef to, int count)
        {
            var (fromType, fromSlot) = ToLocal(from);
            var (toType, toSlot) = ToLocal(to);
            var result = _controller.MoveItem(fromType, fromSlot, toType, toSlot, count);
            return PlayerItemMoveOutcomeConversion.FromController(result, nameof(LocalBlockInventoryOperations), $"{from} -> {to} x{count}");

            #region Internal

            // 番地の写しはここ 1 か所。ブロックは MainSlotCount を足した結合の番地
            // The only address mapping; block refs add MainSlotCount for the combined index
            (LocalMoveInventoryType type, int slot) ToLocal(ItemMoveSlotRef slot)
            {
                return slot.Area switch
                {
                    ItemMoveArea.Grab => (LocalMoveInventoryType.Grab, 0),
                    ItemMoveArea.Main => (LocalMoveInventoryType.MainOrSub, slot.Slot),
                    ItemMoveArea.Equipment => (LocalMoveInventoryType.Equipment, slot.Slot),
                    ItemMoveArea.Block => (LocalMoveInventoryType.MainOrSub, _controller.LocalPlayerInventory.MainSlotCount + slot.Slot),
                    _ => throw new ArgumentOutOfRangeException(nameof(slot), slot.Area, null),
                };
            }

            #endregion
        }

        public void CollectToGrab()
        {
            _controller.CollectItems(LocalMoveInventoryType.Grab, 0);
        }

        public void CollectToBlockSlot(int blockSlot)
        {
            _controller.CollectItems(LocalMoveInventoryType.MainOrSub, _controller.LocalPlayerInventory.MainSlotCount + blockSlot);
        }
    }
}
