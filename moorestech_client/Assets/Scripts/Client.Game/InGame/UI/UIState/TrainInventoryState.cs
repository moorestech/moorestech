using System.Threading;
using Client.Common.Asset;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.Entity.Object;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Block;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.Inventory.Train;
using Client.Input;
using Cysharp.Threading.Tasks;
using Game.Context;
using MessagePack;
using UnityEngine;
using static Server.Event.EventReceive.TrainInventoryUpdateEventPacket;

namespace Client.Game.InGame.UI.UIState
{
    /// <summary>
    /// TODO 実装のほとんどがBlockと同一なので、うまいこと共通化したいなぁ〜〜....
    /// </summary>
    public class TrainInventoryState : IUIState
    {
        private const string AddressablePath = "InGame/UI/Inventory/TrainInventoryView";
        
        private readonly PlayerInventoryViewController _playerInventoryViewController;
        
        private CancellationTokenSource _loadTrainInventoryCts;
        private TrainEntityObject _openTrainEntity;
        private ITrainInventoryView _trainInventoryView;

        
        public TrainInventoryState()
        {
            ClientContext.VanillaApi.Event.SubscribeEventResponse(EventTag, OnOpenableBlockInventoryUpdateEvent);
        }
        
        
        private void OnOpenableBlockInventoryUpdateEvent(byte[] payload)
        {
            if (_trainInventoryView == null) return;
            
            var packet = MessagePackSerializer.Deserialize<TrainInventoryUpdateEventMessagePack>(payload);
            var item = ServerContext.ItemStackFactory.Create(packet.Item.Id, packet.Item.Count);
            _trainInventoryView.UpdateInventorySlot(packet.Slot, item);
        }
        
        
        public void OnEnter(UIStateEnum lastStateEnum)
        {
            if (!BlockClickDetect.TryGetCursorOnComponent(out _openTrainEntity)) return;
            
            LoadTrainInventory().Forget();
            
            // TODO トレインが消えた時に適切に閉じる処理を書く
            
            #region Internal
            
            async UniTask LoadTrainInventory()
            {
                _loadTrainInventoryCts = new CancellationTokenSource();
                
                using var loadedInventory = await AddressableLoader.LoadAsync<GameObject>(AddressablePath);
                if (_loadTrainInventoryCts.IsCancellationRequested) return;
                
                InputManager.MouseCursorVisible(true);
                
                _trainInventoryView = ClientContext.DIContainer.Instantiate(loadedInventory.Asset, _playerInventoryViewController.SubInventoryParent).GetComponent<ITrainInventoryView>();
                _trainInventoryView.Initialize(_openTrainEntity);
                
                var response = await ClientContext.VanillaApi.Response.GetTrainInventory(_openTrainEntity.TrainId, _loadTrainInventoryCts.Token);
                
                if (_loadTrainInventoryCts.IsCancellationRequested) return;
                
                _trainInventoryView.UpdateItemList(response);
                _playerInventoryViewController.SetActive(true);
                _playerInventoryViewController.SetSubInventory(_trainInventoryView);
            }
                
            #endregion
        }
        
        
        public UIStateEnum GetNextUpdate()
        {
            throw new System.NotImplementedException();
        }
        
        public void OnExit()
        {
            _loadTrainInventoryCts?.Cancel();
            
            // ブロックを閉じる設定
            // Close block settings
            ClientContext.VanillaApi.SendOnly.SetOpenCloseTrain(_openTrainEntity.TrainId, false);
            
            // サブインベントリを空にする
            // Set the sub inventory to empty
            _playerInventoryViewController.SetSubInventory(new EmptySubInventory());
            
            // ブロックインベントリを閉じる
            // Close the block inventory
            _playerInventoryViewController.SetActive(false);
            _trainInventoryView?.DestroyUI();
            _trainInventoryView = null;
        }
    }
}