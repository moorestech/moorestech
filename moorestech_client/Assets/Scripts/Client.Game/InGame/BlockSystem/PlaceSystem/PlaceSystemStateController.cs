using Client.Game.InGame.UI.Inventory;
using Game.PlayerInventory.Interface;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public class PlaceSystemStateController
    {
        private readonly PlaceSystemSelector _placeSystemSelector;
        private readonly HotBarView _hotBarView;
        
        private IPlaceSystem _currentPlaceSystem;
        private int _lastSelectHotBarSlot;
        
        public PlaceSystemStateController(HotBarView hotBarView, PlaceSystemSelector placeSystemSelector)
        {
            _hotBarView = hotBarView;
            _placeSystemSelector = placeSystemSelector;
            
            _currentPlaceSystem = _placeSystemSelector.EmptyPlaceSystem;
            Disable();
        }
        
        public void Disable()
        {
            _currentPlaceSystem.Disable();
            _currentPlaceSystem = _placeSystemSelector.EmptyPlaceSystem;
            _lastSelectHotBarSlot = -1;
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
                var selectIndex = _hotBarView.SelectIndex;
                var isSelectSlotChanged = _lastSelectHotBarSlot != selectIndex;
                
                var context = new PlaceSystemUpdateContext(
                    _hotBarView.CurrentItem.Id,
                    isSelectSlotChanged,
                    _lastSelectHotBarSlot,
                    selectIndex
                );
                
                _lastSelectHotBarSlot = selectIndex;
                return context;
            }
            
             #endregion
        }
    }
}