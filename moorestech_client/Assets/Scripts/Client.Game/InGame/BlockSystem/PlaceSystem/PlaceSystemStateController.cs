using System;
using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public class PlaceSystemStateController
    {
        private readonly PlaceSystemSelector _placeSystemSelector;
        private readonly PlacementSelection _placementSelection;

        private IPlaceSystem _currentPlaceSystem;

        // 前回フレームの選択内容（選択変化検知に使う）
        // Previous frame's selection (used to detect selection changes)
        private PlacementSelectionType _lastSelectionType;
        private BlockId? _lastSelectedBlockId;
        private Guid _lastSelectedTrainCarGuid;
        private string _lastSelectedConnectPlaceMode;

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

            // 選択の前回値も初期化し、再Enable直後の最初のフレームでIsSelectionChanged=trueにする
            // Reset previous selection values so the first frame after re-enable reports IsSelectionChanged=true
            _lastSelectionType = PlacementSelectionType.None;
            _lastSelectedBlockId = null;
            _lastSelectedTrainCarGuid = Guid.Empty;
            _lastSelectedConnectPlaceMode = null;
        }

        public void ManualUpdate()
        {
            var updateContext = CreateContext();
            var nextPlaceSystem = _placeSystemSelector.GetCurrentPlaceSystem(updateContext);

            if (_currentPlaceSystem != nextPlaceSystem)
            {
                _currentPlaceSystem.Disable();
                _currentPlaceSystem = nextPlaceSystem;
                _currentPlaceSystem.Enable();
            }

            _currentPlaceSystem.ManualUpdate(updateContext);


            #region Internal

            PlaceSystemUpdateContext CreateContext()
            {
                // 選択内容の変化を検知する（車両プレビューのリセット等に使う）
                // Detect selection changes (used to reset previews such as the train car preview)
                var isSelectionChanged = _lastSelectionType != _placementSelection.SelectionType
                                         || _lastSelectedBlockId != _placementSelection.SelectedBlockId
                                         || _lastSelectedTrainCarGuid != _placementSelection.SelectedTrainCarGuid
                                         || _lastSelectedConnectPlaceMode != _placementSelection.SelectedConnectPlaceMode;

                var context = new PlaceSystemUpdateContext(
                    _placementSelection.SelectionType,
                    _placementSelection.SelectedBlockId,
                    _placementSelection.SelectedTrainCarGuid,
                    _placementSelection.SelectedConnectPlaceMode,
                    isSelectionChanged
                );

                _lastSelectionType = _placementSelection.SelectionType;
                _lastSelectedBlockId = _placementSelection.SelectedBlockId;
                _lastSelectedTrainCarGuid = _placementSelection.SelectedTrainCarGuid;
                _lastSelectedConnectPlaceMode = _placementSelection.SelectedConnectPlaceMode;
                return context;
            }

             #endregion
        }
    }
}
