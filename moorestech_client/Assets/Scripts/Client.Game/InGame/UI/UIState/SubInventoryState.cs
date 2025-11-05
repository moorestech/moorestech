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
using Server.Event.EventReceive.UnifiedInventoryEvent;
using Server.Util.MessagePack;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState
{
    /// <summary>
    /// 統一サブインベントリUIステート（ブロックと列車のインベントリを統一管理）
    /// Unified sub inventory UI state (manages both block and train inventories)
    /// </summary>
    public class SubInventoryState : IUIState
    {
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly PlayerInventoryViewController _playerInventoryViewController;
        
        private IInventorySource _currentSource;
        private ISubInventoryView _currentView;
        private CancellationTokenSource _loadInventoryCts;
        private IDisposable _inventoryRemovedSubscription;
        private IDisposable _blockRemovedSubscription;
        private bool _shouldClose = false;
        
        
        public SubInventoryState(BlockGameObjectDataStore blockGameObjectDataStore, PlayerInventoryViewController playerInventoryViewController)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _playerInventoryViewController = playerInventoryViewController;
            
            // 統一インベントリ更新イベントを購読
            // Subscribe to unified inventory update event
            ClientContext.VanillaApi.Event.SubscribeEventResponse(UnifiedInventoryEventPacket.EventTag, OnUnifiedInventoryEvent);
        }
        
        /// <summary>
        /// インベントリソースを設定してステートを開く
        /// Set inventory source and open state
        /// </summary>
        public void SetInventorySource(IInventorySource source)
        {
            _currentSource = source;
        }
        
        private void OnUnifiedInventoryEvent(byte[] payload)
        {
            if (_currentView == null || _currentSource == null) return;
            
            var packet = MessagePackSerializer.Deserialize<UnifiedInventoryEventMessagePack>(payload);
            
            if (packet.EventType == InventoryEventType.Update)
            {
                var item = ServerContext.ItemStackFactory.Create(packet.Item.Id, packet.Item.Count);
                _currentView.UpdateInventorySlot(packet.Slot, item);
            }
            else if (packet.EventType == InventoryEventType.Remove)
            {
                _shouldClose = true;
            }
        }
        
        public UITransitContext GetNextUpdate()
        {
            if (_shouldClose || InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.OpenInventory.GetKeyDown)
            {
                return new UITransitContext(UIStateEnum.GameScreen);
            }

            return new UITransitContext(UIStateEnum.Current);
        }

        public void OnEnter(UITransitContext context)
        {
            _shouldClose = false;
            
            if (_currentSource == null)
            {
                Debug.LogError("SubInventoryState: インベントリソースが設定されていません");
                return;
            }
            
            LoadInventory().Forget();
            KeyControlDescription.Instance.SetText("Esc: インベントリを閉じる");
            
            #region Internal
            
            async UniTask LoadInventory()
            {
                // インベントリの読み込みをキャンセルするためのトークンソースを生成
                // Generate cancellation token source for inventory loading
                _loadInventoryCts = new CancellationTokenSource();
                
                // Addressableパスを取得してUIをロード
                // Get Addressable path and load UI
                var path = _currentSource.GetAddressablePath();
                if (string.IsNullOrEmpty(path))
                {
                    Debug.LogError("SubInventoryState: Addressableパスが空です");
                    return;
                }
                
                using var loadedInventory = await AddressableLoader.LoadAsync<GameObject>(path);
                if (loadedInventory == null)
                {
                    Debug.LogError($"SubInventoryState: インベントリビューのロードに失敗しました Path:{path}");
                    return;
                }
                
                // キャンセルチェック
                // Check cancellation
                if (_loadInventoryCts.IsCancellationRequested) return;
                
                // ビューコンポーネントを取得
                // Get view component
                var viewType = _currentSource.GetViewType();
                if (!loadedInventory.Asset.TryGetComponent(viewType, out var viewComponent))
                {
                    Debug.LogError($"SubInventoryState: ビューコンポーネントが見つかりません Type:{viewType.Name} Path:{path}");
                    return;
                }
                
                // カーソルを表示
                // Show cursor
                InputManager.MouseCursorVisible(true);
                
                // UIオブジェクトを生成
                // Instantiate UI object
                var instantiatedView = ClientContext.DIContainer.Instantiate(loadedInventory.Asset, _playerInventoryViewController.SubInventoryParent);
                _currentView = instantiatedView.GetComponent<ISubInventoryView>();
                
                // ビューを初期化（型安全な初期化メソッドを呼び出す）
                // Initialize view (call type-safe initialization method)
                InitializeView();
                
                _playerInventoryViewController.SetActive(true);
                _playerInventoryViewController.SetSubInventory(_currentView);
                
                // キャンセルチェック
                // Check cancellation
                if (_loadInventoryCts.IsCancellationRequested) return;
                
                // サーバーにサブスクライブ
                // Subscribe to server
                var inventoryType = _currentSource.GetInventoryType();
                var identifier = _currentSource.GetIdentifier();
                ClientContext.VanillaApi.SendOnly.SubscribeInventory(inventoryType, identifier, true);
                
                // インベントリデータを取得
                // Fetch inventory data
                var response = await _currentSource.FetchInventoryData(_loadInventoryCts.Token);
                if (_loadInventoryCts.IsCancellationRequested) return;
                
                _currentView?.UpdateItemList(response);
                
                // ブロックインベントリの場合、ブロック削除イベントを購読
                // For block inventory, subscribe to block removal event
                if (inventoryType == InventoryType.Block && identifier.BlockPosition != null)
                {
                    var blockPos = identifier.BlockPosition.Vector3Int;
                    _blockRemovedSubscription = _blockGameObjectDataStore.OnBlockRemoved
                        .Subscribe(removedPos =>
                        {
                            if (removedPos == blockPos) _shouldClose = true;
                        });
                }
            }
            
            void InitializeView()
            {
                // ブロックインベントリの場合
                // For block inventory
                if (_currentView is IBlockInventoryView blockView && _currentSource is BlockInventorySource blockSource)
                {
                    if (BlockClickDetect.TryGetCursorOnBlockPosition(out var blockPos))
                    {
                        if (_blockGameObjectDataStore.TryGetBlockGameObject(blockPos, out var blockGameObject))
                        {
                            blockView.Initialize(blockGameObject);
                        }
                    }
                }
                // 列車インベントリの場合
                // For train inventory
                else if (_currentView is Client.Game.InGame.UI.Inventory.Train.ITrainInventoryView trainView && _currentSource is TrainInventorySource)
                {
                    if (BlockClickDetect.TryGetCursorOnComponent(out Client.Game.InGame.Entity.Object.TrainEntityObject trainEntity))
                    {
                        trainView.Initialize(trainEntity);
                    }
                }
            }
            
            #endregion
        }
        
        public void OnExit()
        {
            // キャンセル
            // Cancel
            _loadInventoryCts?.Cancel();
            _loadInventoryCts?.Dispose();
            
            // イベント購読解除
            // Unsubscribe events
            _inventoryRemovedSubscription?.Dispose();
            _blockRemovedSubscription?.Dispose();
            
            // サーバーにアンサブスクライブ
            // Unsubscribe from server
            if (_currentSource != null)
            {
                var inventoryType = _currentSource.GetInventoryType();
                var identifier = _currentSource.GetIdentifier();
                ClientContext.VanillaApi.SendOnly.SubscribeInventory(inventoryType, identifier, false);
            }
            
            // サブインベントリを空にする
            // Set sub inventory to empty
            _playerInventoryViewController.SetSubInventory(new EmptySubInventory());
            
            // インベントリを閉じる
            // Close inventory
            _playerInventoryViewController.SetActive(false);
            _currentView?.DestroyUI();
            _currentView = null;
            _currentSource = null;
        }
    }
}

