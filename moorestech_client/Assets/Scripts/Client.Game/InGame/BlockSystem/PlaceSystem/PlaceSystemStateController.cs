using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public class PlaceSystemStateController
    {
        private readonly PlaceSystemSelector _placeSystemSelector;
        private readonly PlacementSelection _placementSelection;

        private IPlaceSystem _currentPlaceSystem;
        private IPlacementTarget _lastTarget;

        public PlaceSystemStateController(PlaceSystemSelector placeSystemSelector, PlacementSelection placementSelection)
        {
            _placeSystemSelector = placeSystemSelector;
            _placementSelection = placementSelection;

            _currentPlaceSystem = _placeSystemSelector.EmptyPlaceSystem;
            Disable();
        }

        public void Disable()
        {
            _currentPlaceSystem.Disable();
            _currentPlaceSystem = _placeSystemSelector.EmptyPlaceSystem;
            _lastTarget = null;
        }

        public void ManualUpdate()
        {
            var currentTarget = CreateTargetFromSelection();
            var isSelectionChanged = !Equals(_lastTarget, currentTarget);
            _lastTarget = currentTarget;

            var updateContext = new PlaceSystemUpdateContext(currentTarget, isSelectionChanged);
            var nextPlaceSystem = _placeSystemSelector.GetCurrentPlaceSystem(updateContext);

            if (_currentPlaceSystem != nextPlaceSystem)
            {
                _currentPlaceSystem.Disable();
                _currentPlaceSystem = nextPlaceSystem;
                _currentPlaceSystem.Enable();
            }

            _currentPlaceSystem.ManualUpdate(updateContext);

            #region Internal

            // 暫定アダプタ: 共有インスタンスからターゲットを組み立てる（Task 5で遷移payload化して削除）
            // Transitional adapter: build the target from the shared selection (removed in Task 5)
            IPlacementTarget CreateTargetFromSelection()
            {
                switch (_placementSelection.SelectionType)
                {
                    case PlacementSelectionType.Block:
                        return new BlockPlacementTarget(_placementSelection.SelectedBlockId.Value, _placementSelection.SelectedBlockDirection);
                    case PlacementSelectionType.TrainCar:
                        return new TrainCarPlacementTarget(_placementSelection.SelectedTrainCarGuid);
                    case PlacementSelectionType.ConnectTool:
                        return new ConnectToolPlacementTarget(_placementSelection.SelectedConnectPlaceMode);
                    case PlacementSelectionType.Blueprint:
                        return new BlueprintPlacementTarget(_placementSelection.SelectedBlueprintName);
                    case PlacementSelectionType.BlueprintCopy:
                        return new BlueprintCopyToolPlacementTarget();
                    default:
                        return null;
                }
            }

            #endregion
        }
    }
}
