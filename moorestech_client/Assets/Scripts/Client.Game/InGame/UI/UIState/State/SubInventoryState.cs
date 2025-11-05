using System;
using System.Collections.Generic;
using System.Threading;
using Client.Common.Asset;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Block;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Input;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using Game.Context;
using MessagePack;
using Server.Event.EventReceive.UnifiedInventoryEvent;
using Server.Util.MessagePack;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    /// <summary>
    /// 統一サブインベントリUIステート（ブロックと列車のインベントリを統一管理）
    /// Unified sub inventory UI state (manages both block and train inventories)
    /// </summary>
    public class SubInventoryState : IUIState
    {
        private readonly PlayerInventoryViewController _playerInventoryViewController;
        
        private ISubInventorySource _subInventorySource;
        
        private ISubInventoryView _currentView;
        private CancellationTokenSource _loadInventoryCts;
        private bool _shouldClose = false;
        
        
        public SubInventoryState(PlayerInventoryViewController playerInventoryViewController)
        {
            _playerInventoryViewController = playerInventoryViewController;
            
            // 統一インベントリ更新イベントを購読
            // Subscribe to unified inventory update event
            ClientContext.VanillaApi.Event.SubscribeEventResponse(UnifiedInventoryEventPacket.EventTag, OnUnifiedInventoryEvent);
        }
        
        private void OnUnifiedInventoryEvent(byte[] payload)
        {
            if (_currentView == null) return;
            
            var packet = MessagePackSerializer.Deserialize<UnifiedInventoryEventMessagePack>(payload);
            
            if (packet.EventType == InventoryEventType.Update)
            {
                // アイテムを更新
                var item = ServerContext.ItemStackFactory.Create(packet.Item.Id, packet.Item.Count);
                _currentView.UpdateInventorySlot(packet.Slot, item);
            }
            else if (packet.EventType == InventoryEventType.Remove)
            {
                // 開いているインベントリが削除された場合は閉じる
                // Close if the opened inventory is removed
                _shouldClose = true;
            }
        }
        
        public UITransitContext GetNextUpdate()
        {
            if (_shouldClose || InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.OpenInventory.GetKeyDown)
            {
                return new UITransitContext(UIStateEnum.GameScreen);
            }

            return null;
        }

        public void OnEnter(UITransitContext context)
        {
            _shouldClose = false;
            
            // サブインベントリソースを取得
            // Get sub inventory source
            _subInventorySource = context.GetContext<ISubInventorySource>();
            if (_subInventorySource == null)
            {
                Debug.LogError("SubInventoryState: サブインベントリソースが指定されていません");
                return;
            }
            
            // サブインベントリを生成し、データを取得、表示する
            // Create sub inventory, fetch data, and display
            LoadInventory().Forget();
            KeyControlDescription.Instance.SetText("Esc: インベントリを閉じる");
            
            #region Internal
            
            async UniTask LoadInventory()
            {
                _loadInventoryCts = new CancellationTokenSource();
                var ct = _loadInventoryCts.Token;
                
                // UI Prefabをロード
                // Load UI Prefab
                using var loadedInventory = await AddressableLoader.LoadAsync<GameObject>(_subInventorySource.UIPrefabAddressablePath, ct);
                if (loadedInventory == null)
                {
                    Debug.LogError($"SubInventoryState: インベントリビューのロードに失敗しました Path:{_subInventorySource.UIPrefabAddressablePath}");
                    return;
                }
                
                // カーソルを表示
                // Show cursor
                InputManager.MouseCursorVisible(true);
                
                // UIオブジェクトを生成
                // Instantiate UI object
                var instantiatedView = ClientContext.DIContainer.Instantiate(loadedInventory.Asset, _playerInventoryViewController.SubInventoryParent);
                _currentView = instantiatedView.GetComponent<ISubInventoryView>();
                
                // ビューを初期化
                // Initialize view
                _subInventorySource.ExecuteInitialize(_currentView);
                
                _playerInventoryViewController.SetActive(true);
                _playerInventoryViewController.SetSubInventory(_currentView);
                
                // インベントリの更新を購読
                // Subscribe to inventory updates
                ClientContext.VanillaApi.SendOnly.SubscribeInventory(_subInventorySource.InventoryIdentifier, true);
                
                // TODO インベントリデータを取得
                // Fetch inventory data
                _currentView.UpdateItemList(new List<IItemStack>());
            }
            
            #endregion
        }
        
        public void OnExit()
        {
            // キャンセル
            // Cancel
            _loadInventoryCts?.Cancel();
            _loadInventoryCts?.Dispose();
            
            // インベントリ更新の購読を解除
            // Unsubscribe from inventory updates
            ClientContext.VanillaApi.SendOnly.SubscribeInventory(_subInventorySource.InventoryIdentifier, false);
            
            // サブインベントリ登録を解除
            // Unregister sub inventory
            _playerInventoryViewController.SetSubInventory(new EmptySubInventory());
            
            // インベントリを閉じる
            // Close inventory
            _playerInventoryViewController.SetActive(false);
            _currentView?.DestroyUI();
            _currentView = null;
            _subInventorySource = null;
        }
    }
}

