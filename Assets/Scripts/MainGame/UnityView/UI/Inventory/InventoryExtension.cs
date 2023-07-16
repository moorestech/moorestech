using MainGame.UnityView.UI.Inventory.Control;

namespace MainGame.UnityView.UI.Inventory
{
    public static class InventoryExtension
    {
        public static bool IsItemExist(this PlayerInventoryViewModel playerInventoryViewModel,string itemModId,string itemName)
        {
            var itemId = playerInventoryViewModel.ItemConfig.GetItemId(itemModId,itemName);
            foreach (var item in playerInventoryViewModel)
            {
                if (item.Id == itemId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}