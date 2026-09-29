using System.Linq;
using Core.Master;

namespace Client.Game.InGame.UI.Inventory.Main.Operations
{
    internal static class InventoryItemCollector
    {
        public static void Collect(LocalPlayerInventoryController controller, LocalMoveInventoryType targetType, int targetSlot)
        {
            // 少ない順に集積移動（uGUI/Web共通）
            // Gather smallest-first; shared by uGUI/web collect
            var collectTarget = controller.GetItem(targetType, targetSlot);
            if (collectTarget.Id == ItemMaster.EmptyItemId) return;

            // 結合スロット時のみ同index除外
            // Exclude the same index only when the target is combined
            var isCombinedTarget = targetType == LocalMoveInventoryType.MainOrSub;
            var sourceSlots = controller.LocalPlayerInventory
                .Select((item, index) => (item, index))
                .Where(x => x.item.Id == collectTarget.Id)
                .Where(x => !isCombinedTarget || x.index != targetSlot)
                .OrderBy(x => x.item.Count)
                .Select(x => x.index)
                .ToList();

            foreach (var index in sourceSlots)
            {
                var added = collectTarget.AddItem(controller.LocalPlayerInventory[index]);
                var moveCount = controller.LocalPlayerInventory[index].Count - added.RemainderItemStack.Count;

                // 1個も移せない＝集積先が満杯なので終了
                // Zero movable items means the target is full; stop here
                if (moveCount <= 0) break;
                controller.MoveItem(LocalMoveInventoryType.MainOrSub, index, targetType, targetSlot, moveCount);
                collectTarget = added.ProcessResultItemStack;

                // 余りが出たら集積先が満杯なので終了
                // A remainder means the target stack is full; stop here
                if (added.RemainderItemStack.Count != 0) break;
            }
        }
    }
}
