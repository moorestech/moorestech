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
        private readonly PlacementSelection _placementSelection;

        public BuildMenuState(BuildMenuView buildMenuView, PlacementSelection placementSelection)
        {
            _buildMenuView = buildMenuView;
            _placementSelection = placementSelection;
        }

        public void OnEnter(UITransitContext context)
        {
            _buildMenuView.SetActive(true);
            InputManager.MouseCursorVisible(true);
            KeyControlDescription.Instance.SetText("クリック: 設置ブロック選択  B: 閉じる");
        }

        public UITransitContext GetNextUpdate()
        {
            // 選択が確定したら種別に応じて選択状態を設定し設置モードへ遷移する
            // On selection, set the placement selection by entry type and transition to placement mode
            if (_buildMenuView.TryConsumeSelectedEntry(out var entry))
            {
                switch (entry.EntryType)
                {
                    case PlacementSelectionType.Block:
                        _placementSelection.SetSelectedBlock(entry.BlockId);
                        break;
                    case PlacementSelectionType.TrainCar:
                        _placementSelection.SetSelectedTrainCar(entry.TrainCarGuid);
                        break;
                    case PlacementSelectionType.ConnectTool:
                        _placementSelection.SetSelectedConnectTool(entry.ConnectPlaceMode);
                        break;
                }
                return new UITransitContext(UIStateEnum.PlaceBlock);
            }

            if (InputManager.UI.CloseUI.GetKeyDown || HybridInput.GetKeyDown(KeyCode.B)) return new UITransitContext(UIStateEnum.GameScreen);
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
