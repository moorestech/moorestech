using System;
using System.Collections.Generic;
using Client.Game.Common;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState.State.NestedPause;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Game.InGame.UI.UIState.State.Skit;
using Client.Game.Skit;
using Client.Input;
using Client.Skit.UI;
using UniRx;

namespace Client.Game.InGame.UI.UIState.State
{
    // フォーカス復帰時の入場やり直しは持たない。スキット中のカーソル表示はサブステート入場だけで確定し、列車HUDのようなカメラ再取得が無い
    // No focus-restore hook: the skit's cursor is settled by sub-state entry alone and has no camera re-acquisition like the train HUD
    public class SkitState : IUIState, INestedPauseScreenState
    {
        private readonly SkitManager _skitManager;
        private readonly PlayerInventoryViewController _playerInventoryViewController;
        private readonly NestedPauseSubStateController _subStateController;
        private readonly Subject<Unit> _onPresentationChanged = new();
        
        // Web配信用の入れ子state窓口
        // Window for the web side to publish the nested state
        public NestedPauseSubStateEnum SubState => _subStateController.CurrentState;
        public IObservable<Unit> OnPresentationChanged => _onPresentationChanged;
        
        public SkitState(SkitManager skitManager, PlayerInventoryViewController playerInventoryViewController, PauseMenuStateService pauseMenuStateService)
        {
            _skitManager = skitManager;
            _playerInventoryViewController = playerInventoryViewController;
            // 所有者専用の入れ子ステートマシンなのでDI登録せずここでnewする（前例: TrainHUDScreenState）
            // A nested state machine owned exclusively here, so it is newed directly instead of DI-registered (precedent: TrainHUDScreenState)
            _subStateController = new NestedPauseSubStateController(new SkitGameScreenSubState(skitManager), pauseMenuStateService);
            _subStateController.OnStateChanged.Subscribe(OnSubStateChanged);
        }
        
        // ポーズ中はスキットの入力口をstore側で閉じる。可否の正はAllowedIntentsのまま1箇所に保つ
        // Pausing closes the skit's input paths in the store, keeping AllowedIntents the single authority
        private void OnSubStateChanged(NestedPauseSubStateEnum subState)
        {
            SkitPresentationStateStore.Instance.SetInputSuspended(subState == NestedPauseSubStateEnum.PauseMenuScreen);
            _onPresentationChanged.OnNext(Unit.Default);
        }
        
        public void OnEnter(UITransitContext context)
        {
            // インベントリが開いている場合は閉じる
            // Close the inventory if it is open
            if (context.LastStateEnum == UIStateEnum.PlayerInventory || context.LastStateEnum == UIStateEnum.SubInventory)
            {
                _playerInventoryViewController.SetActive(false);
            }
            
            // スキット状態へ遷移
            // Switch the game state to Skit
            GameStateController.ChangeState(GameStateType.Skit);
            
            // 再生サブステートから開始
            // Start from the playing sub-state
            _subStateController.StartSubState();
        }
        
        public UITransitContext GetNextUpdate()
        {
            // スキット終了はメニュー表示中でも優先し、GameScreenへ戻す（ADR 0035）
            // Skit end takes priority even while the menu shows, returning to GameScreen (ADR 0035)
            if (!_skitManager.IsPlayingSkit) return new UITransitContext(UIStateEnum.GameScreen);
            
            _subStateController.Update();
            return null;
        }
        
        public void OnExit()
        {
            // 入れ子サブステートを終了
            // Tear down the nested sub-state
            _subStateController.ShutdownSubState();
            
            // スキット終了時はカーソルを非表示に戻す
            // Hide the cursor again when the skit ends
            InputManager.MouseCursorVisible(false);
            
            // ゲーム状態をInGameに戻す
            // Return the game state to InGame
            GameStateController.ChangeState(GameStateType.InGame);
        }
        
        public bool RequestClosePauseMenu()
        {
            return _subStateController.RequestClosePauseMenu();
        }
        
        // 表示中のサブステートが宣言したヒントをそのまま返す
        // Return the hints declared by whichever sub-state is currently showing
        public IReadOnlyList<KeyHint> GetKeyHints()
        {
            return _subStateController.GetKeyHints();
        }
    }
}
