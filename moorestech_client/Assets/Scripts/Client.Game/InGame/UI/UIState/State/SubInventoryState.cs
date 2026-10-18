using System.Collections.Generic;
using System;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState.State.CancelInput;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Input;
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
        private readonly LocalPlayerInventoryController _localPlayerInventoryController;
        private readonly RightShortPressInputService _rightShortPressInputService;
        private readonly SubInventoryOutOfReachDetector _outOfReachDetector = new();

        private CancellationTokenSource _loadInventoryCts;
        private SubInventoryLoader _loader;
        private bool _shouldClose = false;

        // 開いているサブと発生元の公開口
        // Read access to the open sub and its source
        public SubInventoryModel CurrentSubInventory { get; private set; }
        public ISubInventorySource CurrentSubInventorySource { get; private set; }

        // スロット単位の更新通知(変更毎に発火)
        // Per-slot update notification (fired on change)
        public IObservable<Unit> OnSubInventoryUpdated => _onSubInventoryUpdated;
        private readonly Subject<Unit> _onSubInventoryUpdated = new();


        public SubInventoryState(LocalPlayerInventoryController localPlayerInventoryController, RightShortPressInputService rightShortPressInputService)
        {
            _localPlayerInventoryController = localPlayerInventoryController;
            _rightShortPressInputService = rightShortPressInputService;

            // 統一インベントリ更新イベントを購読
            // Subscribe to unified inventory update event
            ClientContext.VanillaApi.Event.SubscribeEventResponse(UnifiedInventoryEventPacket.EventTag, OnUnifiedInventoryEvent);
        }

        private void OnUnifiedInventoryEvent(byte[] payload)
        {
            var packet = MessagePackSerializer.Deserialize<UnifiedInventoryEventMessagePack>(payload);
            if (_shouldClose || _loader == null) return; // silent-ok: 閉鎖中の通知 / 開いている対象の通知だけを適用する

            var disposition = _loader.HandleEvent(packet.Identifier, packet.EventType);
            if (disposition == InventoryEventDisposition.ApplyItemUpdate)
            {
                // アイテムを更新
                var item = ServerContext.ItemStackFactory.Create(packet.Item.Id, packet.Item.Count);
                CurrentSubInventory.SetItem(packet.Slot, item);

                // 外部購読者(Web UI等)へ通知
                // Notify external subscribers (e.g. Web UI)
                _onSubInventoryUpdated.OnNext(Unit.Default);
            }
            else if (disposition == InventoryEventDisposition.Remove)
            {
                // 開いているインベントリが削除された場合は閉じる
                // Close if the opened inventory is removed
                _shouldClose = true;
                _loadInventoryCts.Cancel();
            }
        }

        public UITransitContext GetNextUpdate()
        {
            var isPointerOverUi = UiPointerHitTest.IsPointerOverAnyUi();
            var isRightShortPressed = _rightShortPressInputService.TryConsumeShortPressOutsideUi(isPointerOverUi);
            if (_shouldClose || InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.OpenInventory.GetKeyDown || isRightShortPressed || IsOutOfReach())
            {
                return new UITransitContext(UIStateEnum.GameScreen);
            }

            return null;

            #region Internal

            // 自機も列車も動くため毎フレーム測る。距離はFで開くときと同じ近傍探索で決める
            // Both the player and trains move, so it is measured every frame with the same nearby query F-open uses
            bool IsOutOfReach()
            {
                if (CurrentSubInventorySource == null) return false;
                var playerPosition = PlayerSystemContainer.Instance.PlayerObjectController.Position;
                return _outOfReachDetector.IsOutOfReach(CurrentSubInventorySource, playerPosition);
            }

            #endregion
        }

        public void OnEnter(UITransitContext context)
        {
            // 他UIState滞在中は右短押しがpollされないため、復帰直後の古い押下状態を破棄する
            // Right short press isn't polled while another UIState is active, so discard any stale press state on return
            _rightShortPressInputService.ResetPressState();

            _shouldClose = false;

            // サブインベントリソースを取得
            // Get sub inventory source
            CurrentSubInventorySource = context.GetContext<ISubInventorySource>();
            if (CurrentSubInventorySource == null)
            {
                Debug.LogError("SubInventoryState: サブインベントリソースが指定されていません");
                return;
            }

            // サブインベントリを生成し、データを取得、表示する
            // Create sub inventory, fetch data, and display
            _loadInventoryCts = new CancellationTokenSource();
            _loader = new SubInventoryLoader(new SubInventoryServerQuery(), CurrentSubInventorySource, _loadInventoryCts.Token);
            _loader.OnSubInventoryLoaded.Subscribe(ApplyInventory);
            InputManager.MouseCursorVisible(true);
            ClientContext.VanillaApi.SendOnly.SubscribeInventory(CurrentSubInventorySource.InventoryIdentifier, true);
            _loader.RequestRefresh();

        }

        private void ApplyInventory(SubInventoryModel model)
        {
            CurrentSubInventory = model;
            _localPlayerInventoryController.SetSubInventory(model);
            _onSubInventoryUpdated.OnNext(Unit.Default);
        }

        public void OnExit()
        {
            // キャンセル・破棄済みCTSを次回へ持ち越さない
            // Cancel and never carry a disposed CTS into the next Enter
            _loadInventoryCts?.Cancel();
            _loadInventoryCts?.Dispose();
            _loadInventoryCts = null;
            _loader = null;

            // OnEnterがソース未指定で早期returnしていた場合はここで終える
            // Bail out here if OnEnter returned early with no source
            if (CurrentSubInventorySource == null)
            {
                CurrentSubInventory = null;
                return;
            }

            // インベントリ更新の購読を解除
            // Unsubscribe from inventory updates
            ClientContext.VanillaApi.SendOnly.SubscribeInventory(CurrentSubInventorySource.InventoryIdentifier, false);

            // サブインベントリ登録を解除
            // Unregister sub inventory
            _localPlayerInventoryController.SetSubInventory(new EmptySubInventory());
            CurrentSubInventory = null;
            CurrentSubInventorySource = null;
        }

        public bool AllowsSprintToggle() => false;

        public bool LocksPlayerMovement()
        {
            return false;
        }

        public IReadOnlyList<KeyHint> GetKeyHints()
        {
            return SubInventoryStateHints.Hints;
        }
    }
}
