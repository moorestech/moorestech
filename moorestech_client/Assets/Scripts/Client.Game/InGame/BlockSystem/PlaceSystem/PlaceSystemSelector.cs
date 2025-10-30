using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Empty;
using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public class PlaceSystemSelector
    {
        public readonly EmptyPlaceSystem EmptyPlaceSystem;
        private readonly CommonBlockPlaceSystem _commonBlockPlaceSystem;
        
        public PlaceSystemSelector(CommonBlockPlaceSystem commonBlockPlaceSystem)
        {
            EmptyPlaceSystem = new EmptyPlaceSystem();
            _commonBlockPlaceSystem = commonBlockPlaceSystem;
        }
        
        public IPlaceSystem GetCurrentPlaceSystem(PlaceSystemUpdateContext context)
        {
            if (MasterHolder.BlockMaster.IsBlock(context.HoldingItemId))
            {
                return _commonBlockPlaceSystem;
            }
            
            return EmptyPlaceSystem;
        }
    }
}