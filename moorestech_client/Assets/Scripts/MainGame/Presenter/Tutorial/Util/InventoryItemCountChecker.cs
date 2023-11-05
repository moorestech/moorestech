using Core.Item.Config;
using MainGame.UnityView.UI.Inventory.Control;

namespace MainGame.Presenter.Tutorial.Util
{
    /// <summary>
    ///     チュートリアルで使う「このアイテムがインベントリ内にn個あるか」みたいなのをチェックする
    /// </summary>
    public class InventoryItemCountChecker
    {
        private readonly IItemConfig _itemConfig;
        private readonly PlayerInventoryViewModel _playerInventoryViewModel;

        public InventoryItemCountChecker(PlayerInventoryViewModel playerInventoryViewModel, IItemConfig itemConfig)
        {
            _playerInventoryViewModel = playerInventoryViewModel;
            _itemConfig = itemConfig;
        }

        /// <summary>
        ///     そのアイテムがインベントリ内に指定個数あるか
        /// </summary>
        /// <returns></returns>
        public bool ExistItemMainInventory(string modId, string itemName, int itemCount)
        {
            var inventoryItemCount = 0;
            var itemId = _itemConfig.GetItemId(modId, itemName);
            //この関数はUpdateで呼ばれる可能性が高いのでLINQは使わない
            foreach (var item in _playerInventoryViewModel.MainInventory)
                if (item.Id == itemId)
                    inventoryItemCount += item.Count;
            return itemCount <= inventoryItemCount;
        }
    }
}