using System.Collections.Generic;
using Client.Game.Common;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState.State.Skit;
using Client.Game.Skit;
using Client.Input;

namespace Client.Game.InGame.UI.UIState.State
{
    public class SkitState : IUIState
    {
        private readonly SkitManager _skitManager;
        private readonly PlayerInventoryViewController _playerInventoryViewController;
        private readonly SkitScreenUIStateController _subStateController;
        
        public SkitState(SkitManager skitManager, PlayerInventoryViewController playerInventoryViewController, SkitScreenUIStateController subStateController)
        {
            _skitManager = skitManager;
            _playerInventoryViewController = playerInventoryViewController;
            _subStateController = subStateController;
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
            
            // 再生サブステートから開始（カーソル表示はサブステート側が担う）
            // Start from the playing sub-state (the sub-state owns cursor visibility)
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
            // 入れ子サブステートを終了（開いていればポーズメニューを閉じる）
            // Tear down the nested sub-state (closes the pause menu if open)
            _subStateController.ShutdownSubState();
            
            // スキット終了時はカーソルを非表示に戻す
            // Hide the cursor again when the skit ends
            InputManager.MouseCursorVisible(false);
            
            // ゲーム状態をInGameに戻す
            // Return the game state to InGame
            GameStateController.ChangeState(GameStateType.InGame);
        }
        
        public IReadOnlyList<KeyHint> GetKeyHints()
        {
            return System.Array.Empty<KeyHint>();
        }
    }
}
