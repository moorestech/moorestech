using System;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Ui.State.ItemMove;
using Client.Ui.State.Items;
using Core.Item.Interface;
using UniRx;
using UnityEngine;

namespace Client.Ui.State.Inventory.Actions
{
    /// <summary>
    /// 本番の持ち物の口。コントローラの移動（サーバーへ送る）と集めを呼び、結果の列挙は共有の変換で移動の結果へ写す
    /// Production inventory port; calls the controller's move (sent to the server) and collect, mapping its result enum through the shared conversion
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
            // 判定はコントローラの MoveItem 1 か所。結果の写しは共有の変換へ
            // The controller's MoveItem is the sole judge; the result mapping is the shared conversion
            var result = _controller.MoveItem(fromType, fromSlot, toType, toSlot, count);
            return PlayerItemMoveOutcomeConversion.FromController(result, nameof(LocalPlayerInventoryOperations), $"{fromType}:{fromSlot} -> {toType}:{toSlot} x{count}");
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
