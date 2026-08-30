using System.Collections.Generic;
using Mooresmaster.Localization.Generated;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Client.Game.InGame.UI.UIState.State.CancelInput;
using Client.Input;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class PlayerInventoryState : IUIState
    {
        private readonly RecipeViewerView _recipeViewerView;
        private readonly LocalPlayerInventoryController _localPlayerInventoryController;
        private readonly LocalPlayerEquipment _localPlayerEquipment;
        private readonly PlayerInventoryViewController _playerInventoryViewController;
        private readonly RightShortPressInputService _rightShortPressInputService;

        private CancellationTokenSource _cancellationTokenSource;

        public PlayerInventoryState(RecipeViewerView recipeViewerView, PlayerInventoryViewController playerInventoryViewController, LocalPlayerInventoryController localPlayerInventoryController, LocalPlayerEquipment localPlayerEquipment, InitialHandshakeResponse handshakeResponse, RightShortPressInputService rightShortPressInputService)
        {
            _recipeViewerView = recipeViewerView;
            _playerInventoryViewController = playerInventoryViewController;
            _localPlayerInventoryController = localPlayerInventoryController;
            _localPlayerEquipment = localPlayerEquipment;
            _rightShortPressInputService = rightShortPressInputService;

            _playerInventoryViewController.SetActive(false); //TODO この辺のオンオフをまとめたい
            _recipeViewerView.SetActive(false);

            //インベントリの初期設定
            ApplyInventoryResponse(handshakeResponse.Inventory);
        }
        
        public UITransitContext GetNextUpdate()
        {
            // 毎フレーム押下を追跡するため先に評価する（短絡で押下開始を取りこぼさない）
            // Evaluate first so the press is tracked every frame (short-circuiting would miss the press start)
            var isRightShortPressed = _rightShortPressInputService.TryConsumeShortPressOutsideUi();

            // Rでリサーチツリーへ、Tab/ESC/パネル外の右短押しでゲーム画面へ戻る
            // Go to research tree with R, or back to game screen with Tab/ESC/right short press outside UI
            if (HybridInput.GetKeyDown(KeyCode.R)) return new UITransitContext(UIStateEnum.ResearchTree);
            if (InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.OpenInventory.GetKeyDown || isRightShortPressed) return new UITransitContext(UIStateEnum.GameScreen);

            return null;
        }

        public void OnEnter(UITransitContext context)
        {
            // 他UIState滞在中は右短押しがpollされないため、復帰直後の古い押下状態を破棄する
            // Right short press isn't polled while another UIState is active, so discard any stale press state on return
            _rightShortPressInputService.ResetPressState();

            _recipeViewerView.SetActive(true);
            _playerInventoryViewController.SetActive(true);
            _playerInventoryViewController.SetSubInventory(new EmptySubInventory());

            _cancellationTokenSource = new CancellationTokenSource();
            UpdatePlayerInventory(_cancellationTokenSource.Token).Forget();

            InputManager.MouseCursorVisible(true);
        }
        
        public void OnExit()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = null;
            
            _recipeViewerView.SetActive(false);
            _playerInventoryViewController.SetActive(false);
        }
        
        /// <summary>
        ///     基本的にプレイヤーのインベントリはイベントによって逐次更新データが送られてくるため、これをする必要がない
        ///     ただ、更新データが何らかの原因で送られてこなかったり、適用できなかった時のために、バックアップとしてインベントリが開いた際は更新をかけるようにしている
        /// </summary>
        private async UniTask UpdatePlayerInventory(CancellationToken ct)
        {
            var invResponse = await ClientContext.VanillaApi.Response.GetMyPlayerInventory(ct);

            ApplyInventoryResponse(invResponse);
        }

        // 装備も同じ応答に同梱されるため、初期適用とバックアップ更新の両方で一緒に反映する
        // Equipment rides on the same response, so it is applied together on both the initial and the backup update
        private void ApplyInventoryResponse(PlayerInventoryResponse response)
        {
            _localPlayerInventoryController.SetMainInventory(response.MainInventory);
            _localPlayerInventoryController.SetGrabItem(response.GrabItem);
            _localPlayerEquipment.Initialize(response.Equipment, response.SelectedEquipmentIndex);
        }

        public IReadOnlyList<KeyHint> GetKeyHints()
        {
            return PlayerInventoryStateHints.Hints;
        }
    }

    internal static class PlayerInventoryStateHints
    {
        public static readonly IReadOnlyList<KeyHint> Hints = new[]
        {
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.Tab, LocalizationKeys.Ui.KeyHint.Text.CloseInventory),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.R, LocalizationKeys.Ui.KeyHint.Text.ResearchTree),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.ShiftLeftClick, LocalizationKeys.Ui.KeyHint.Text.BulkMove),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.RightClick, LocalizationKeys.Ui.KeyHint.Text.HalveOrPlaceOne),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.LeftDrag, LocalizationKeys.Ui.KeyHint.Text.DistributeEvenly),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.DoubleClick, LocalizationKeys.Ui.KeyHint.Text.GatherSameItem),
        };
    }
}
