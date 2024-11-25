using System.Threading;
using Client.Common.Asset;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Block;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Input;
using Cysharp.Threading.Tasks;
using Game.Context;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState
{
    public class BlockInventoryState : IUIState
    {
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly PlayerInventoryViewController _playerInventoryViewController;
        
        private CancellationTokenSource _loadBlockInventoryCts;
        private IBlockInventoryVIew _iBlockInventoryVIew;
        private Vector3Int _openBlockPos;
        
        public BlockInventoryState(BlockGameObjectDataStore blockGameObjectDataStore, PlayerInventoryViewController playerInventoryViewController)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _playerInventoryViewController = playerInventoryViewController;
            
            ClientContext.VanillaApi.Event.SubscribeEventResponse(OpenableBlockInventoryUpdateEventPacket.EventTag, OnOpenableBlockInventoryUpdateEvent);
        }
        
        public UIStateEnum GetNextUpdate()
        {
            if (InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.GameScreen;
            
            return UIStateEnum.Current;
        }
        
        // ReSharper disable Unity.PerformanceAnalysis
        public void OnEnter(UIStateEnum lastStateEnum)
        {
            BlockGameObject blockGameObject = null;
            if (!IsExistBlock())
            {
                return;
            }
            
            _loadBlockInventoryCts = new CancellationTokenSource();
            LoadBlockInventory().Forget();
            
            #region Internal
            
            bool IsExistBlock()
            {
                if (!BlockClickDetect.TryGetCursorOnBlockPosition(out _openBlockPos))
                {
                    // TODO ログ基盤に入れる
                    Debug.LogError("開いたブロックの座標が取得できませんでした。UIステートに不具合があります。");
                    return false;
                }
                
                if (!_blockGameObjectDataStore.TryGetBlockGameObject(_openBlockPos, out blockGameObject))
                {
                    // TODO ログ基盤に入れる
                    Debug.LogError("開いたブロックの情報が取得できませんでした。");
                    return false;
                }
                
                var blockMaster = blockGameObject.BlockMasterElement;
                var inventoryPath = blockMaster.BlockUIAddressablesPath;
                if (string.IsNullOrEmpty(inventoryPath))
                {
                    // TODO ログ基盤に入れる
                    Debug.LogError($"開いたブロックのインベントリのパスがありませんでした。 Guid:{blockMaster.BlockGuid} Name:{blockMaster.Name}");

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
                if (!loadedInventory.Asset.TryGetComponent(out IBlockInventoryVIew _))
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
                _iBlockInventoryVIew = ClientContext.DIContainer.Instantiate(loadedInventory.Asset, _playerInventoryViewController.SubInventoryParent).GetComponent<IBlockInventoryVIew>();
                _iBlockInventoryVIew.Initialize(blockGameObject);
                _playerInventoryViewController.SetActive(true);
                _playerInventoryViewController.SetSubInventory(_iBlockInventoryVIew);
                
                // check cts
                if (_loadBlockInventoryCts.IsCancellationRequested) return;
                
                // ブロックインベントリのデータを取得する
                // Get block inventory data
                ClientContext.VanillaApi.SendOnly.SetOpenCloseBlock(_openBlockPos, true);
                var response = await ClientContext.VanillaApi.Response.GetBlockInventory(_openBlockPos, _loadBlockInventoryCts.Token);
                _iBlockInventoryVIew?.UpdateItemList(response);
            }
            
            #endregion
        }
        
        public void OnExit()
        {
            _loadBlockInventoryCts?.Cancel();
            
            // ブロックを閉じる設定
            // Close block settings
            ClientContext.VanillaApi.SendOnly.SetOpenCloseBlock(_openBlockPos, false);
            
            // サブインベントリを空にする
            // Set the sub inventory to empty
            _playerInventoryViewController.SetSubInventory(new EmptySubInventory());
            
            // ブロックインベントリを閉じる
            // Close the block inventory
            _playerInventoryViewController.SetActive(false);
            _iBlockInventoryVIew?.DestroyUI();
            _iBlockInventoryVIew = null;
        }
        
        private void OnOpenableBlockInventoryUpdateEvent(byte[] payload)
        {
            if (_iBlockInventoryVIew == null) return;
            
            var packet = MessagePackSerializer.Deserialize<OpenableBlockInventoryUpdateEventMessagePack>(payload);
            var item = ServerContext.ItemStackFactory.Create(packet.Item.Id, packet.Item.Count);
            _iBlockInventoryVIew.UpdateInventorySlot(packet.Slot, item);
        }
    }
}