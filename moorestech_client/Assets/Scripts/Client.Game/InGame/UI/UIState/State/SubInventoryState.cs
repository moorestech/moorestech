using System.Collections.Generic;
using Mooresmaster.Localization.Generated;
using System;
using System.Threading;
using Client.Common.Asset;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState.State.CancelInput;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Input;
using Cysharp.Threading.Tasks;
using Game.Context;
using MessagePack;
using UniRx;
using Server.Event.EventReceive.UnifiedInventoryEvent;
using Server.Util.MessagePack;
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
        private readonly RightShortPressInputService _rightShortPressInputService;

        private ISubInventorySource _subInventorySource;

        private ISubInventoryView _currentView;
        private CancellationTokenSource _loadInventoryCts;
        private bool _shouldClose = false;

        // 開いているサブと発生元の公開口
        // Read access to the open sub and its source
        public ISubInventory CurrentSubInventory => _currentView;
        public ISubInventorySource CurrentSubInventorySource => _subInventorySource;

        // スロット単位の更新通知(変更毎に発火)
        // Per-slot update notification (fired on change)
        public IObservable<Unit> OnSubInventoryUpdated => _onSubInventoryUpdated;
        private readonly Subject<Unit> _onSubInventoryUpdated = new();


        public SubInventoryState(PlayerInventoryViewController playerInventoryViewController, RightShortPressInputService rightShortPressInputService)
        {
            _playerInventoryViewController = playerInventoryViewController;
            _rightShortPressInputService = rightShortPressInputService;

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

                // 外部購読者(Web UI等)へ通知
                // Notify external subscribers (e.g. Web UI)
                _onSubInventoryUpdated.OnNext(Unit.Default);
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
            var isRightShortPressed = _rightShortPressInputService.TryConsumeShortPressOutsideUi();
            if (_shouldClose || InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.OpenInventory.GetKeyDown || isRightShortPressed)
            {
                return new UITransitContext(UIStateEnum.GameScreen);
            }

            return null;
        }

        public void OnEnter(UITransitContext context)
        {
            // 他UIState滞在中は右短押しがpollされないため、復帰直後の古い押下状態を破棄する
            // Right short press isn't polled while another UIState is active, so discard any stale press state on return
            _rightShortPressInputService.ResetPressState();

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

                // インベントリデータを取得
                // Fetch inventory data and initialize
                var inventoryResponse = await ClientContext.VanillaApi.Response.GetInventory(_subInventorySource.InventoryIdentifier, ct);

                // UIオブジェクトを生成し初期化
                // Instantiate UI object
                var instantiatedView = ClientDIContext.DIContainer.Instantiate(loadedInventory.Asset, _playerInventoryViewController.SubInventoryParent);
                _currentView = instantiatedView.GetComponent<ISubInventoryView>();
                _subInventorySource.ExecuteInitialize(_currentView, inventoryResponse);

                // インベントリビューを表示
                // Show inventory view
                _playerInventoryViewController.SetActive(true);
                _playerInventoryViewController.SetSubInventory(_currentView);

                // インベントリの更新を購読
                // Subscribe to inventory updates
                ClientContext.VanillaApi.SendOnly.SubscribeInventory(_subInventorySource.InventoryIdentifier, true);

                // ロード完了を外部購読者（Web UI など）へ通知する
                // Notify external subscribers (e.g. Web UI) that loading has finished
                _onSubInventoryUpdated.OnNext(Unit.Default);
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

        public IReadOnlyList<KeyHint> GetKeyHints()
        {
            return SubInventoryStateHints.Hints;
        }
    }

    internal static class SubInventoryStateHints
    {
        public static readonly IReadOnlyList<KeyHint> Hints = new[]
        {
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.Tab, LocalizationKeys.Ui.KeyHint.Text.Close),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.ShiftLeftClick, LocalizationKeys.Ui.KeyHint.Text.BulkMove),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.RightClick, LocalizationKeys.Ui.KeyHint.Text.HalveOrPlaceOne),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.LeftDrag, LocalizationKeys.Ui.KeyHint.Text.DistributeEvenly),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.DoubleClick, LocalizationKeys.Ui.KeyHint.Text.GatherSameItem),
        };
    }
}
