using System;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Ui.State.ItemMove;
using Client.Ui.State.Items;
using Core.Item.Interface;
using UniRx;

namespace Client.Ui.State.Inventory.Actions
{
    /// <summary>
    /// 本番の持ち物の口。コントローラの検証つき移動（サーバーへ送る）と集めを呼び、拒否の文字列はここで 1 回だけ結果の列挙へ写す
    /// Production inventory port; calls the controller's validated move (sent to the server) and collect, mapping deny strings to the outcome enum once here
    /// </summary>
    public sealed class LocalPlayerInventoryOperations : IPlayerInventoryOperations
    {
        private readonly LocalPlayerInventoryController _controller;
        private readonly LocalPlayerEquipment _equipment;

        public IObservable<Unit> OnGrabChanged => _controller.OnGrabChanged;

        public LocalPlayerInventoryOperations(LocalPlayerInventoryController controller, LocalPlayerEquipment equipment)
        {
            _controller = controller;
            _equipment = equipment;
        }

        public PlayerInventorySnapshot ReadSnapshot()
        {
            // メインはメインの枠だけ（サブインベントリはブロック側の番地で S4-alpha が読む）
            // Main covers only the main slots; the sub-inventory is read through block refs by S4-alpha
            var inventory = _controller.LocalPlayerInventory;
            var main = new SlotItem[inventory.MainSlotCount];
            for (var i = 0; i < main.Length; i++) main[i] = ToSlotItem(inventory[i]);
            var equipment = new SlotItem[_equipment.Slots.Count];
            for (var i = 0; i < equipment.Length; i++) equipment[i] = ToSlotItem(_equipment.Slots[i]);
            return new PlayerInventorySnapshot(main, ReadGrab(), equipment);
        }

        public SlotItem ReadGrab()
        {
            return ToSlotItem(_controller.GrabInventory);
        }

        public PlayerItemMoveOutcome MoveToGrab(PlayerSlotRef from, int count)
        {
            return Move(ToLocalType(from), from.Slot, LocalMoveInventoryType.Grab, 0, count);
        }

        public PlayerItemMoveOutcome MoveFromGrab(PlayerSlotRef to, int count)
        {
            return Move(LocalMoveInventoryType.Grab, 0, ToLocalType(to), to.Slot, count);
        }

        public PlayerItemMoveOutcome MoveBetweenSlots(PlayerSlotRef from, PlayerSlotRef to, int count)
        {
            return Move(ToLocalType(from), from.Slot, ToLocalType(to), to.Slot, count);
        }

        public void CollectToGrab()
        {
            _controller.CollectItems(LocalMoveInventoryType.Grab, 0);
        }

        public void CollectToSlot(PlayerSlotRef slot)
        {
            _controller.CollectItems(ToLocalType(slot), slot.Slot);
        }

        private PlayerItemMoveOutcome Move(LocalMoveInventoryType fromType, int fromSlot, LocalMoveInventoryType toType, int toSlot, int count)
        {
            if (_controller.TryMoveItem(fromType, fromSlot, toType, toSlot, count, out var denyReason)) return PlayerItemMoveOutcome.Moved;

            // コントローラの拒否コード（Web の inventory.move_item と同じ綴り）を結果へ写すのはここだけ
            // The only place the controller's deny code (same spelling as Web inventory.move_item) becomes an outcome
            return denyReason switch
            {
                "empty_slot" => PlayerItemMoveOutcome.SourceEmpty,
                "insufficient_count" => PlayerItemMoveOutcome.SourceCountShort,
                _ => throw new ArgumentOutOfRangeException(nameof(denyReason), denyReason, "LocalPlayerInventoryController.TryMoveItem の未知の拒否コード。PlayerItemMoveOutcome に足すこと"),
            };
        }

        private static LocalMoveInventoryType ToLocalType(PlayerSlotRef slot)
        {
            return slot.IsEquipment ? LocalMoveInventoryType.Equipment : LocalMoveInventoryType.MainOrSub;
        }

        private static SlotItem ToSlotItem(IItemStack stack)
        {
            return new SlotItem(stack.Id, stack.Count);
        }
    }
}
