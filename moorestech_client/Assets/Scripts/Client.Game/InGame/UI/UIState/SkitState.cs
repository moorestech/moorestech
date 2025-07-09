using Client.Game.Common;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.Skit;
using Client.Input;

namespace Client.Game.InGame.UI.UIState
{
    public class SkitState : IUIState
    {
        private readonly SkitManager _skitManager;
        private readonly PlayerInventoryViewController _playerInventoryViewController;
        
        public SkitState(SkitManager skitManager, PlayerInventoryViewController playerInventoryViewController)
        {
            _skitManager = skitManager;
            _playerInventoryViewController = playerInventoryViewController;
        }
        
        public void OnEnter(UIStateEnum lastStateEnum)
        {
            // スキット中はカーソルを表示してUIを操作できるようにする
            InputManager.MouseCursorVisible(true);
            
            // インベントリが開いている場合は閉じる
            if (lastStateEnum == UIStateEnum.PlayerInventory || lastStateEnum == UIStateEnum.BlockInventory)
            {
                _playerInventoryViewController.SetActive(false);
            }
            
            // GameStateControllerでスキット状態に遷移（ホットバーの非表示を含む）
            GameStateController.ChangeState(GameStateType.Skit);
            
            KeyControlDescription.Instance.SetText("");
        }
        
        public UIStateEnum GetNextUpdate()
        {
            if (_skitManager.IsPlayingSkit) return UIStateEnum.Current;
            return UIStateEnum.GameScreen;
        }
        
        public void OnExit()
        {
            // スキット終了時はカーソルを非表示に戻す
            InputManager.MouseCursorVisible(false);
            
            // ゲーム状態をInGameに戻す
            GameStateController.ChangeState(GameStateType.InGame);
        }
    }
}