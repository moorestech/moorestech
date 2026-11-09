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
    /// 本番の持ち物の口。コントローラの移動（サーバーへ送る）と集めを呼び、その結果の列挙はここで 1 回だけ移動の結果へ写す
    /// Production inventory port; calls the controller's move (sent to the server) and collect, mapping its result enum to the move outcome once here
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
            // 判定はコントローラの MoveItem 1 か所。何もしない移動は送られないので開発者ログへ出して成功と区別する
            // The controller's MoveItem is the sole judge; a no-effect move is not sent, so it is logged for developers apart from success
            var result = _controller.MoveItem(fromType, fromSlot, toType, toSlot, count);
            if (result == LocalItemMoveResult.NoEffect) Debug.LogWarning($"[LocalPlayerInventoryOperations] move has no effect: {fromType}:{fromSlot} -> {toType}:{toSlot} x{count}");

            // コントローラの結果を移動の結果へ写すのはここだけ
            // The only place the controller's result becomes a move outcome
            return result switch
            {
                LocalItemMoveResult.Moved => PlayerItemMoveOutcome.Moved,
                LocalItemMoveResult.NoEffect => PlayerItemMoveOutcome.NoEffect,
                LocalItemMoveResult.SourceEmpty => PlayerItemMoveOutcome.SourceEmpty,
                LocalItemMoveResult.SourceCountShort => PlayerItemMoveOutcome.SourceCountShort,
                _ => throw new ArgumentOutOfRangeException(nameof(result), result, "LocalItemMoveResult の未知の値。PlayerItemMoveOutcome に足すこと"),
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
