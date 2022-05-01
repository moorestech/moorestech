using MainGame.Network.Event;
using MainGame.UnityView.UI.Inventory.Control;
using UnityEngine;
using VContainer;

namespace MainGame.Presenter.Inventory.Receive
{
    /// <summary>
    /// プレイヤーインベントリのGrabbedItem（インベントリでスロットをクリックしたときにマウスカーソルについてくる画像）の画像や数字の更新を行います
    /// </summary>
    public class GrabbedItemImagePresenter : MonoBehaviour
    {
        private PlayerInventoryViewModelController _playerInventoryViewModel;

        [Inject]
        public void Construct(GrabInventoryUpdateEvent grabInventoryUpdateEvent,PlayerInventoryViewModelController playerInventoryViewModel)
        {
            grabInventoryUpdateEvent.OnGrabInventoryUpdateEvent += UpdateGrabbedItemImage;
            _playerInventoryViewModel = playerInventoryViewModel;
        }

        private void UpdateGrabbedItemImage(GrabInventoryUpdateEventProperties properties)
        {
            var id = properties.ItemStack.ID;
            var count = properties.ItemStack.Count;
            _playerInventoryViewModel.SetGrabItem(id,count);
        }
    }
}