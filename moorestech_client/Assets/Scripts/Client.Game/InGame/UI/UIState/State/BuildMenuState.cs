using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
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
            // カーソル適用はBuildViewModeController委譲（FPS中も解放）
            // Cursor visibility is applied by BuildViewModeController (freed in the menu even during FPS)
            _buildViewModeController.OnEnterBuildState(UIStateEnum.BuildMenu);
            _buildMenuView.SetActive(true);
            KeyControlDescription.Instance.SetText("クリック: 設置ブロック選択  B: 閉じる");
        }

        public UITransitContext GetNextUpdate()
        {
            if (_buildMenuView.TryConsumeSelectedEntry(out var entry))
            {
                ApplySelection(entry.Target);
                return Leave(UIStateEnum.PlaceBlock);
            }

            if (InputManager.UI.CloseUI.GetKeyDown || HybridInput.GetKeyDown(KeyCode.B)) return Leave(UIStateEnum.GameScreen);
            if (InputManager.UI.OpenInventory.GetKeyDown) return Leave(UIStateEnum.PlayerInventory);

            return null;
        }

        // 暫定: 旧共有インスタンスへ橋渡し（Task 5で遷移payloadに置換して削除）
        // Transitional bridge to the legacy shared selection (replaced by transition payload in Task 5)
        private void ApplySelection(IPlacementTarget target)
        {
            switch (target)
            {
                case BlockPlacementTarget block:
                    _placementSelection.SetSelectedBlock(block.BlockId, block.PickedDirection);
                    break;
                case TrainCarPlacementTarget trainCar:
                    _placementSelection.SetSelectedTrainCar(trainCar.TrainCarGuid);
                    break;
                case ConnectToolPlacementTarget connectTool:
                    _placementSelection.SetSelectedConnectTool(connectTool.PlaceMode);
                    break;
                case BlueprintPlacementTarget blueprint:
                    _placementSelection.SetSelectedBlueprint(blueprint.BlueprintName);
                    break;
                case BlueprintCopyToolPlacementTarget:
                    _placementSelection.SetSelectedBlueprintCopyTool();
                    break;
            }
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
