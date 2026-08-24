using System.Collections.Generic;
using Client.Game.Common;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.Skit;
using Client.Input;

namespace Client.Game.InGame.UI.UIState.State
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
        
        public void OnEnter(UITransitContext context)
        {
            // スキット中はカーソルを表示してUIを操作できるようにする
            InputManager.MouseCursorVisible(true);

            // インベントリが開いている場合は閉じる
            if (context.LastStateEnum == UIStateEnum.PlayerInventory || context.LastStateEnum == UIStateEnum.SubInventory)
            {
                _playerInventoryViewController.SetActive(false);
            }

            // スキット状態へ遷移
            GameStateController.ChangeState(GameStateType.Skit);

        }

        public UITransitContext GetNextUpdate()
        {
            if (_skitManager.IsPlayingSkit) return null;
            
            return new UITransitContext(UIStateEnum.GameScreen);
        }
        
        public void OnExit()
        {
            // スキット終了時はカーソルを非表示に戻す
            InputManager.MouseCursorVisible(false);
            
            // ゲーム状態をInGameに戻す
            GameStateController.ChangeState(GameStateType.InGame);
        }

        public IReadOnlyList<KeyHint> GetKeyHints()
        {
            return System.Array.Empty<KeyHint>();
        }
    }
}
