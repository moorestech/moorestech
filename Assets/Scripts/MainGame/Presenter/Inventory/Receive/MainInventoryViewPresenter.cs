using System;
using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.UnityView.UI.Inventory.Control;
using UniRx;
using VContainer.Unity;

namespace MainGame.Presenter.Inventory.Receive
{
    //IInitializableがないとDIコンテナ作成時にインスタンスが生成されないので実装しておく
    public class MainInventoryViewPresenter : IInitializable
    {
        public IObservable<(int slot, ItemStack item)> OnUpdateInventory=> _updateInventorySubject;
        private readonly Subject<(int,ItemStack)> _updateInventorySubject = new();
        
        private readonly PlayerInventoryViewModelController _playerInventoryViewModelController;

        public MainInventoryViewPresenter(ReceiveMainInventoryEvent receiveMainInventoryEvent,PlayerInventoryViewModelController playerInventoryViewModelController,PlayerInventoryViewModel playerInventoryViewModel)
        {
            _playerInventoryViewModelController = playerInventoryViewModelController;
            receiveMainInventoryEvent.OnMainInventoryUpdateEvent +=UpdateInventory;
            receiveMainInventoryEvent.OnMainInventorySlotUpdateEvent +=UpdateSlotInventory;
        }

        private void UpdateInventory(MainInventoryUpdateProperties properties)
        {
            for (int i = 0; i < properties.ItemStacks.Count; i++)
            {
                var id = properties.ItemStacks[i].ID;
                var count = properties.ItemStacks[i].Count;
                var slot = i;
                //View側を更新する
                _playerInventoryViewModelController.SetInventoryItem(slot,id,count);
                _updateInventorySubject.OnNext((slot,properties.ItemStacks[i]));
            }
        }

        private void UpdateSlotInventory(MainInventorySlotUpdateProperties properties)
        {
            //View側を更新する
            _playerInventoryViewModelController.SetInventoryItem(properties.SlotId,properties.ItemStack.ID,properties.ItemStack.Count);
            _updateInventorySubject.OnNext((properties.SlotId,properties.ItemStack));
        }
        public void Initialize() { }
    }
}