using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    public class TrainCarPlaceSystem : IPlaceSystem
    {
        private readonly ITrainCarPlacementDetector _detector;
        private readonly TrainCarPreviewController _previewController;
        
        public TrainCarPlaceSystem(ITrainCarPlacementDetector detector, TrainCarPreviewController previewController)
        {
            _detector = detector;
            _previewController = previewController;
        }

        public void Enable()
        {
            _previewController.SetActive(true);
        }

        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            if (!_detector.TryDetect(context.HoldingItemId, out var hit))
            {
                _previewController.SetActive(false);
                return;
            }
            _previewController.SetActive(true);
            _previewController.ShowPreview(context.HoldingItemId ,hit.PreviewPosition, hit.PreviewRotation, hit.IsPlaceable);

            if (!hit.IsPlaceable)
            {
                return;
            }

            if (InputManager.Playable.ScreenLeftClick.GetKeyUp)
            {
                ClientContext.VanillaApi.SendOnly.PlaceTrainOnRail(hit.Specifier, context.CurrentSelectHotbarSlotIndex);
            }
        }

        public void Disable()
        {
            _previewController.SetActive(false);
        }
    }
}