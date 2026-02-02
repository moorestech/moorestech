using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail
{
    public class TrainRailPlaceSystem : IPlaceSystem
    {
        private readonly TrainRailPlaceSystemService _trainRailPlaceSystemService;
        public TrainRailPlaceSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController)
        {
            _trainRailPlaceSystemService = new TrainRailPlaceSystemService(mainCamera, previewBlockController);
        }
        
        public void Enable()
        {
        }
        
        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            _trainRailPlaceSystemService.ManualUpdate(context);
        }
        
        public void Disable()
        {
            _trainRailPlaceSystemService.Disable();
        }
    }
}