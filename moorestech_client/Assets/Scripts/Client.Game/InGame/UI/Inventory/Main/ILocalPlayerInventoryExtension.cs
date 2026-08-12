// [uGUI廃止Phase1] Web UI移行済みのため未メンテ・描画恒久停止。Phase2で削除予定（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] Unmaintained; rendering permanently disabled after the Web UI migration. Slated for deletion in Phase2 (docs/webui/ugui-retirement-plan.md)
using Core.Master;

namespace Client.Game.InGame.UI.Inventory.Main
{
    public static class ILocalPlayerInventoryExtension
    {
        public static int GetMainInventoryItemCount(this ILocalPlayerInventory localPlayerInventory, ItemId itemId)
        {
            var count = 0;
            for (var i = 0; i < localPlayerInventory.MainSlotCount; i++)
            {
                if (localPlayerInventory[i].Id == itemId)
                {
                    count += localPlayerInventory[i].Count;
                }
            }

            return count;
        }
    }
}
