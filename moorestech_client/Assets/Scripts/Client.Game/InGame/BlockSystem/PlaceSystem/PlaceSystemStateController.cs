using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Empty;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Game.PlayerInventory.Interface;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public class PlaceSystemStateController
    {
        private readonly EmptyPlaceSystem _emptyPlaceSystem;
        private readonly CommonBlockPlaceSystem _commonBlockPlaceSystem;
        
        private readonly HotBarView _hotBarView;
        private readonly ILocalPlayerInventory _localPlayerInventory;

        
        private IPlaceSystem _currentPlaceSystem;
        private int _lastSelectHotBarSlot;
        
        public PlaceSystemStateController(HotBarView hotBarView, ILocalPlayerInventory localPlayerInventory, CommonBlockPlaceSystem commonBlockPlaceSystem)
        {
            _hotBarView = hotBarView;
            _localPlayerInventory = localPlayerInventory;
            _commonBlockPlaceSystem = commonBlockPlaceSystem;
            _emptyPlaceSystem = new EmptyPlaceSystem();
            
            _currentPlaceSystem = _emptyPlaceSystem;
            Disable();
        }
        
        public void Disable()
        {
            _currentPlaceSystem.Disable();
            _currentPlaceSystem = _emptyPlaceSystem;
            _lastSelectHotBarSlot = -1;
        }
        
        public void ManualUpdate()
        {
            var updateContext = CreateContext();
            var nextPlaceSystem = GetCurrentPlaceSystem(updateContext);
            
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
                var itemId = _localPlayerInventory[PlayerInventoryConst.HotBarSlotToInventorySlot(selectIndex)].Id;
                
                var context = new PlaceSystemUpdateContext(
                    itemId,
                    isSelectSlotChanged,
                    _lastSelectHotBarSlot,
                    selectIndex
                );
                
                _lastSelectHotBarSlot = selectIndex;
                return context;
            }
            
            IPlaceSystem GetCurrentPlaceSystem(PlaceSystemUpdateContext context)
            {
                if (!MasterHolder.BlockMaster.IsBlock(context.HoldingItemId))
                    return _emptyPlaceSystem;
                
                return _commonBlockPlaceSystem;
            }
            
             #endregion
        }
    }
}