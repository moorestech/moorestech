using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.KeyControl;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class BuildMenuState : IUIState
    {
        private readonly BuildMenuView _buildMenuView;
        private readonly BlockPlacementSelection _blockPlacementSelection;

        public BuildMenuState(BuildMenuView buildMenuView, BlockPlacementSelection blockPlacementSelection)
        {
            _buildMenuView = buildMenuView;
            _blockPlacementSelection = blockPlacementSelection;
        }

        public void OnEnter(UITransitContext context)
        {
            _buildMenuView.SetActive(true);
            InputManager.MouseCursorVisible(true);
            KeyControlDescription.Instance.SetText("クリック: 設置ブロック選択  B: 閉じる");
        }

        public UITransitContext GetNextUpdate()
        {
            // 選択が確定したら設置モードへ遷移する
            // Transition to placement mode once a block is selected
            if (_buildMenuView.TryConsumeSelectedBlock(out var selectedBlockId))
            {
                _blockPlacementSelection.SetSelectedBlock(selectedBlockId);
                return new UITransitContext(UIStateEnum.PlaceBlock);
            }

            if (InputManager.UI.CloseUI.GetKeyDown || UnityEngine.Input.GetKeyDown(KeyCode.B)) return new UITransitContext(UIStateEnum.GameScreen);
            if (InputManager.UI.OpenInventory.GetKeyDown) return new UITransitContext(UIStateEnum.PlayerInventory);

            return null;
        }

        public void OnExit()
        {
            _buildMenuView.SetActive(false);
            InputManager.MouseCursorVisible(false);
        }
    }
}
