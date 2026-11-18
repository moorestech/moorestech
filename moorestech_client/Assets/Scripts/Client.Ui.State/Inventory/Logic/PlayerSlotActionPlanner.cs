using Client.Ui.State.ItemMove;

namespace Client.Ui.State.Inventory.Logic
{
    /// <summary>
    /// 持ち物・装備・ホットバーの 3 パネル共通のスロット操作を、操作の瞬間の在庫で計画する（Web slotActions の計画部分）
    /// Plans the slot gestures shared by all three panels from the inventory at gesture time (planning half of Web slotActions)
    /// </summary>
    public static class PlayerSlotActionPlanner
    {
        public static PlayerSlotActionPlan PlanLeftDown(PlayerSlotContext context, ItemMoveSlotRef target, bool shiftKey)
        {
            if (!context.Inventory.TryResolveSlot(target, out var slot)) return new StaleSlotIgnoredPlan(target);
            return PlayerSlotPlanner.PlanLeftClick(target, slot, shiftKey, context);
        }

        public static PlayerSlotActionPlan PlanRightDown(PlayerInventorySnapshot inventory, ItemMoveSlotRef target)
        {
            if (!inventory.TryResolveSlot(target, out var slot)) return new StaleSlotIgnoredPlan(target);
            return new ItemActionsPlan(PlayerSlotPlanner.PlanRightClick(target, slot, inventory.Grab.Count));
        }

        // 右ドラッグ進入は掴み中だけ 1 個置く。空手で半分取りを連鎖させない
        // A right-drag enter places one only while holding; never chain split pickups empty-handed
        public static PlayerSlotActionPlan PlanRightEnter(PlayerInventorySnapshot inventory, ItemMoveSlotRef target)
        {
            // silent-ok: 空手の右ドラッグは何もしない仕様（Web・uGUI と同じ） / 持ち手が空なのはプレイヤーに見えている
            if (inventory.Grab.Count <= 0) return ItemActionsPlan.Empty;
            if (!inventory.TryResolveSlot(target, out var slot)) return new StaleSlotIgnoredPlan(target);
            return new ItemActionsPlan(PlayerSlotPlanner.PlanRightClick(target, slot, inventory.Grab.Count));
        }

        // 左ボタンのまま入ったスロットは分割ドラッグの行き先候補（行き先の判定はセッションとコマンド側）
        // A slot entered with the left button held is a split-drag candidate (the session and command decide)
        public static PlayerSlotActionPlan PlanLeftEnter(PlayerInventorySnapshot inventory, ItemMoveSlotRef target)
        {
            if (!inventory.TryResolveSlot(target, out _)) return new StaleSlotIgnoredPlan(target);
            return new EnterSplitDragPlan(target);
        }

        public static PlayerSlotActionPlan PlanDoubleClick(PlayerInventorySnapshot inventory, ItemMoveSlotRef target)
        {
            if (!inventory.TryResolveSlot(target, out _)) return new StaleSlotIgnoredPlan(target);
            return new ItemActionsPlan(PlayerSlotPlanner.PlanDoubleClick(target));
        }
    }
}
