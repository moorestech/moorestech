using System;
using System.Threading;
using Client.Common.Asset;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Block;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.KeyControl;
using Client.Input;
using Cysharp.Threading.Tasks;
using Game.Context;
using MessagePack;
using Server.Event.EventReceive;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState
{
    public class BlockInventoryState : IUIState
    {
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly PlayerInventoryViewController _playerInventoryViewController;
        
        private bool _isBlockRemove = false;
        
        private CancellationTokenSource _loadBlockInventoryCts;
        private IBlockInventoryView _blockInventoryView;
        private Vector3Int _openBlockPos;
        private IDisposable _blockRemovedSubscription;
        
        
        public BlockInventoryState(BlockGameObjectDataStore blockGameObjectDataStore, PlayerInventoryViewController playerInventoryViewController)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _playerInventoryViewController = playerInventoryViewController;
            
            ClientContext.VanillaApi.Event.SubscribeEventResponse(OpenableBlockInventoryUpdateEventPacket.EventTag, OnOpenableBlockInventoryUpdateEvent);
        }
        
        public UIStateEnum GetNextUpdate()
        {
            if (_isBlockRemove || InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.GameScreen;
            
            return UIStateEnum.Current;
        }
        
        // ReSharper disable Unity.PerformanceAnalysis
        public void OnEnter(UIStateEnum lastStateEnum)
        {
            _isBlockRemove = false;
            if (!IsExistBlock(out var blockGameObject))
            {
                return;
            }
            
            _loadBlockInventoryCts = new CancellationTokenSource();
            LoadBlockInventory().Forget();
            
            KeyControlDescription.Instance.SetText("Esc: インベントリを閉じる");
            
            #region Internal
            
            bool IsExistBlock(out BlockGameObject block)
            {
                block = null;
                if (!BlockClickDetect.TryGetCursorOnBlockPosition(out _openBlockPos))
                {
                    Debug.LogError("開いたブロックの座標が取得できませんでした。UIステートに不具合があります。"); // TODO ログ基盤に入れる
                    return false;
                }
                
                if (!_blockGameObjectDataStore.TryGetBlockGameObject(_openBlockPos, out block))
                {
                    Debug.LogError("開いたブロックの情報が取得できませんでした。");
                    return false;
                }
                
                var inventoryPath = block.BlockMasterElement.BlockUIAddressablesPath;
                if (string.IsNullOrEmpty(inventoryPath))
                {
                    Debug.LogError($"開こうとしたブロックインベントリのAddressableパスが指定されていません。 Guid:{block.BlockMasterElement.BlockGuid} Name:{block.BlockMasterElement.Name}");

                    return false;
                }
                
                return true;
            }
            
            async UniTask LoadBlockInventory()
            {
                //ブロックインベントリのビューを設定する
                var blockMaster = blockGameObject.BlockMasterElement;
                var path = blockMaster.BlockUIAddressablesPath;
                using var loadedInventory = await AddressableLoader.LoadAsync<GameObject>(path);
                if (loadedInventory == null)
                {
                    // TODO ログ基盤に入れる
                    Debug.LogError($"ブロックインベントリのビューが取得できませんでした。 Guid:{blockMaster.BlockGuid} Name:{blockMaster.Name} Path:{path}");
                    return;
                }
                if (!loadedInventory.Asset.TryGetComponent(out IBlockInventoryView _))
                {
                    // TODO ログ基盤に入れる
                    Debug.LogError($"ブロックインベントリのビューにコンポーネントがついていませんでした。 Guid:{blockMaster.BlockGuid} Name:{blockMaster.Name} Path:{path}");
                    return;
                }
                
                // check cts
                if (_loadBlockInventoryCts.IsCancellationRequested) return;
                
                // カーソルを表示する
                // Show cursor
                InputManager.MouseCursorVisible(true);
                
                // UIのオブジェクトを生成し、オンにする
                // Generate and turn on the UI object
                _blockInventoryView = ClientContext.DIContainer.Instantiate(loadedInventory.Asset, _playerInventoryViewController.SubInventoryParent).GetComponent<IBlockInventoryView>();
                _blockInventoryView.Initialize(blockGameObject);
                _playerInventoryViewController.SetActive(true);
                _playerInventoryViewController.SetSubInventory(_blockInventoryView);
                
                // check cts
                if (_loadBlockInventoryCts.IsCancellationRequested) return;
                
                // ブロックインベントリのデータを取得する
                // Get block inventory data
                ClientContext.VanillaApi.SendOnly.SetOpenCloseBlock(_openBlockPos, true);
                var response = await ClientContext.VanillaApi.Response.GetBlockInventory(_openBlockPos, _loadBlockInventoryCts.Token);
                _blockInventoryView?.UpdateItemList(response);
                // ブロック削除イベントを購読
                _blockRemovedSubscription = _blockGameObjectDataStore.OnBlockRemoved
                    .Subscribe(removedPos =>
                    {
                        // 開いているブロックが削除されたため、UIを閉じる
                        if (removedPos == _openBlockPos) _isBlockRemove = true;
                    });
            }
            
            #endregion
        }
        
        public void OnExit()
        {
            _loadBlockInventoryCts?.Cancel();
            
            // ブロック削除イベントの購読を解除
            _blockRemovedSubscription?.Dispose();
            
            // ブロックを閉じる設定
            // Close block settings
            ClientContext.VanillaApi.SendOnly.SetOpenCloseBlock(_openBlockPos, false);
            
            // サブインベントリを空にする
            // Set the sub inventory to empty
            _playerInventoryViewController.SetSubInventory(new EmptySubInventory());
            
            // ブロックインベントリを閉じる
            // Close the block inventory
            _playerInventoryViewController.SetActive(false);
            _blockInventoryView?.DestroyUI();
            _blockInventoryView = null;
        }
        
        private void OnOpenableBlockInventoryUpdateEvent(byte[] payload)
        {
            if (_blockInventoryView == null) return;
            
            var packet = MessagePackSerializer.Deserialize<OpenableBlockInventoryUpdateEventMessagePack>(payload);
            var item = ServerContext.ItemStackFactory.Create(packet.Item.Id, packet.Item.Count);
            _blockInventoryView.UpdateInventorySlot(packet.Slot, item);
        }
    }
}