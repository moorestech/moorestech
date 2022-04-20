using MainGame.UnityView.UI.Inventory.Element;
using UnityEngine;
using VContainer;

namespace MainGame.Presenter.Inventory
{
    /// <summary>
    /// プレイヤーインベントリのGrabbedItem（インベントリでスロットをクリックしたときにマウスカーソルについてくる画像）の画像や数字の更新を行います
    /// </summary>
    public class PlayerInventoryGrabbedItemImageSet : MonoBehaviour
    {
        [SerializeField] private InventoryItemSlot grabbedItem;
        private ItemImages _itemImages;

        [Inject]
        public void Construct(ItemImages itemImages)
        {
            _itemImages = itemImages;
            //todo もったアイテムの更新イベントをサブスクライブする
        }
    }
}