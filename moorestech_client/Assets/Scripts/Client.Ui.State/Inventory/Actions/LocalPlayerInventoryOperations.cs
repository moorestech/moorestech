using System;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Ui.State.ItemMove;
using Client.Ui.State.Items;
using Core.Item.Interface;
using Core.Master;
using UniRx;
using UnityEngine;

namespace Client.Ui.State.Inventory.Actions
{
    /// <summary>
    /// 本番の持ち物の口。コントローラの検証つき移動（サーバーへ送る）と集めを呼び、拒否の列挙はここで 1 回だけ結果の列挙へ写す
    /// Production inventory port; calls the controller's validated move (sent to the server) and collect, mapping deny reasons to the outcome enum once here
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
            // コントローラが黙って何もしない移動は送らず、開発者ログへ出して成功と区別する
            // A move the controller would silently skip is not sent; it goes to the developer log, distinct from success
            if (IsNoEffectMove())
            {
                Debug.LogWarning($"[LocalPlayerInventoryOperations] move has no effect: {fromType}:{fromSlot} -> {toType}:{toSlot} x{count}");
                return PlayerItemMoveOutcome.NoEffect;
            }
            if (_controller.TryMoveItem(fromType, fromSlot, toType, toSlot, count, out var denyReason)) return PlayerItemMoveOutcome.Moved;

            // コントローラの拒否理由を結果へ写すのはここだけ
            // The only place the controller's deny reason becomes an outcome
            return denyReason switch
            {
                LocalItemMoveDenyReason.SourceEmpty => PlayerItemMoveOutcome.SourceEmpty,
                LocalItemMoveDenyReason.SourceCountShort => PlayerItemMoveOutcome.SourceCountShort,
                _ => throw new ArgumentOutOfRangeException(nameof(denyReason), denyReason, "LocalItemMoveDenyReason の未知の値。PlayerItemMoveOutcome に足すこと"),
            };

            #region Internal

            // コントローラの MoveItem が黙って return する条件（0 個以下・別 ID への部分移動）。空・不足は TryMoveItem の拒否に任せる
            // Conditions where the controller's MoveItem silently returns (count<=0, partial move onto another id); empty/short stay TryMoveItem denials
            bool IsNoEffectMove()
            {
                var from = _controller.GetItem(fromType, fromSlot);
                if (from.Id == ItemMaster.EmptyItemId || from.Count < count) return false;
                if (count <= 0) return true;
                var to = _controller.GetItem(toType, toSlot);
                return to.Id != ItemMaster.EmptyItemId && to.Id != from.Id && count != from.Count;
            }

            #endregion
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
