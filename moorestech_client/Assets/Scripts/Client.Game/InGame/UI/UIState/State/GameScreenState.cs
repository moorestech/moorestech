using System.Collections.Generic;
using Mooresmaster.Localization.Generated;
using System;
using Client.Game.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState.State.CameraPolicy;
using Client.Game.InGame.UI.UIState.State.Hotbar;
using Client.Game.InGame.UI.UIState.State.PlacementPick;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Game.Skit;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class GameScreenState : IUIState, IApplicationFocusRestorer
    {
        private readonly SkitManager _skitManager;
        private readonly GameScreenSubInventoryInteractService _subInventoryInteractService;
        private readonly RideVehicleInputService _rideVehicleInputService;
        private readonly PlacementTargetPickService _placementTargetPickService;
        private readonly UiStateCameraPolicyService _cameraPolicyService;
        private readonly HotbarTapInputService _hotbarInputService;

        public GameScreenState(
            SkitManager skitManager,
            GameScreenSubInventoryInteractService subInventoryInteractService,
            RideVehicleInputService rideVehicleInputService,
            PlacementTargetPickService placementTargetPickService,
            UiStateCameraPolicyService cameraPolicyService,
            HotbarTapInputService hotbarInputService)
        {
            _skitManager = skitManager;
            _subInventoryInteractService = subInventoryInteractService;
            _rideVehicleInputService = rideVehicleInputService;
            _placementTargetPickService = placementTargetPickService;
            _cameraPolicyService = cameraPolicyService;
            _hotbarInputService = hotbarInputService;
        }

        public UITransitContext GetNextUpdate()
        {
            // 遷移判定より先に左Alt自由カーソルの入力を処理する
            // Handle the left-Alt free-cursor input before any transition check
            _cameraPolicyService.UpdateGameplayFreeCursorInput();

            if (InputManager.UI.OpenInventory.GetKeyDown) return new UITransitContext(UIStateEnum.PlayerInventory);
            if (InputManager.UI.OpenMenu.GetKeyDown) return new UITransitContext(UIStateEnum.PauseMenu);

            // 列車に乗り込む範囲＋E押下を 1 行で判定し、TrainHUDScreen へ遷移する。
            // One-line check for "in ride range + interact key pressed", transits to TrainHUDScreen.
            if (_rideVehicleInputService.TryGetInteractTransit(out var rideContext)) return rideContext;

            // ブロックや列車とインタラクトしたか
            if (_subInventoryInteractService.TryGetSubInventoryInteractObject(out var context)) return context;

            // ミドルクリックで設置物をスポイトし配置モードへ入る
            // Middle-click eyedrops a placed object and enters placement mode
            if (_placementTargetPickService.TryPickTargetUnderCursor(out var pickedTarget))
                return new UITransitContext(UIStateEnum.PlaceBlock, UITransitContextContainer.Create(new PlacementSelection(pickedTarget, PlacementOrigin.NonHotbar)));

            // キー/Web選択で建築モードへ遷移
            // A digit key or a web-originated selection enters build mode holding the assigned placement target
            var hotbarTapOutcome = _hotbarInputService.ResolveGameScreenTap(out var hotbarTransit);
            switch (hotbarTapOutcome)
            {
                case HotbarTapOutcome.EnterBuildMode: return hotbarTransit;
                case HotbarTapOutcome.None: break;
                // ゲーム画面では建築モードの離脱・持ち替えは起こりえない
                // Leaving build mode and swapping targets cannot occur on the game screen
                case HotbarTapOutcome.ExitBuildMode:
                case HotbarTapOutcome.SwappedTarget:
                default: throw new ArgumentOutOfRangeException(nameof(hotbarTapOutcome), hotbarTapOutcome, null);
            }

            if (InputManager.UI.BlockDelete.GetKeyDown) return new UITransitContext(UIStateEnum.DeleteBar);
            if (_skitManager.IsPlayingSkit) return new UITransitContext(UIStateEnum.Story);
            
            //TODO InputSystemのリファクタ対象
            if (HybridInput.GetKeyDown(KeyCode.B)) return new UITransitContext(UIStateEnum.BuildMenu);
            if (HybridInput.GetKeyDown(KeyCode.T)) return new UITransitContext(UIStateEnum.ChallengeList);
            if (HybridInput.GetKeyDown(KeyCode.R)) return new UITransitContext(UIStateEnum.ResearchTree);
            if (HybridInput.GetKeyDown(KeyCode.F3)) return new UITransitContext(UIStateEnum.Debug);

            return null;
        }

        public void OnEnter(UITransitContext context)
        {
            // 他UIState滞在中は数字キーがpollされないため、復帰直後の古い押下状態を破棄する
            // Digit keys aren't polled while another UIState is active, so discard any stale press state on return
            _hotbarInputService.ResetKeyState();

            // 通常時はカーソル固定・回転有効
            // Lock cursor and enable rotation in gameplay
            _cameraPolicyService.EnterGameplay();

            // 旧uGUIのHUD表示をGameScreen復帰時に同期する
            // Sync legacy uGUI HUD visibility when returning to GameScreen.
            GameStateController.ChangeState(GameStateType.InGame);

        }

        public void OnExit()
        {
            // 次のUIが背後のカメラ回転を継承しないよう停止する
            // Stop look rotation so the next UI does not inherit background camera movement
            _cameraPolicyService.ExitToNeutral();
        }

        public void RestoreAfterApplicationFocus()
        {
            _cameraPolicyService.RestoreAfterApplicationFocus();
        }

        public IReadOnlyList<KeyHint> GetKeyHints()
        {
            return GameScreenStateHints.Hints;
        }
    }

    // ADR-0032: ゲーム画面のヒント。移動・左クリック・ESC・デバッグキー・T・Eは載せない
    // ADR-0032: game screen hints; movement, left click, ESC, debug keys, T and E are excluded
    internal static class GameScreenStateHints
    {
        public static readonly IReadOnlyList<KeyHint> Hints = new[]
        {
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.Tab, LocalizationKeys.Ui.KeyHint.Text.Inventory),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.Digits, LocalizationKeys.Ui.KeyHint.Text.BuildShortcut),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.B, LocalizationKeys.Ui.KeyHint.Text.BuildMenu),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.G, LocalizationKeys.Ui.KeyHint.Text.DeleteMode),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.R, LocalizationKeys.Ui.KeyHint.Text.ResearchTree),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.V, LocalizationKeys.Ui.KeyHint.Text.ToggleView),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.LeftAltHold, LocalizationKeys.Ui.KeyHint.Text.FreeCursor),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.LeftAltMiddleClick, LocalizationKeys.Ui.KeyHint.Text.PickPlacedObject),
        };
    }
}
