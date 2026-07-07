using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.Control.BuildView;
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
        private readonly BuildViewModeController _buildViewModeController;

        public BuildMenuState(BuildMenuView buildMenuView, PlacementSelection placementSelection, BuildViewModeController buildViewModeController)
        {
            _buildMenuView = buildMenuView;
            _placementSelection = placementSelection;
            _buildViewModeController = buildViewModeController;
        }

        public void OnEnter(UITransitContext context)
        {
            // カーソル表示はBuildViewModeControllerが適用する（FPS中もメニューではカーソル解放）
            // Cursor visibility is applied by BuildViewModeController (freed in the menu even during FPS)
            _buildViewModeController.OnEnterBuildState(UIStateEnum.BuildMenu);
            _buildMenuView.SetActive(true);
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
                    case PlacementSelectionType.Blueprint:
                        _placementSelection.SetSelectedBlueprint(entry.BlueprintName);
                        break;
                    case PlacementSelectionType.BlueprintCopy:
                        _placementSelection.SetSelectedBlueprintCopyTool();
                        break;
                }
                return Leave(UIStateEnum.PlaceBlock);
            }

            if (InputManager.UI.CloseUI.GetKeyDown || HybridInput.GetKeyDown(KeyCode.B)) return Leave(UIStateEnum.GameScreen);
            if (InputManager.UI.OpenInventory.GetKeyDown) return Leave(UIStateEnum.PlayerInventory);

            return null;
        }

        // 遷移確定をコントローラへ通知してから遷移する（セッション終了判定はコントローラ側）
        // Notify the controller before transiting; it decides whether the session ends
        private UITransitContext Leave(UIStateEnum next)
        {
            _buildViewModeController.OnLeaveBuildState(next);
            return new UITransitContext(next);
        }

        public void OnExit()
        {
            _buildMenuView.SetActive(false);
        }
    }
}
