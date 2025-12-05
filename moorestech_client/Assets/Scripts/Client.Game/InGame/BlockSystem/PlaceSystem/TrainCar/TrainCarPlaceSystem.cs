using System.Linq;
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
            var onRailPreview = PlaceTrainOnRail(context);
            var onExistingTrainPreview = PlaceTrainOnExistingTrain(context);
            
            if (!onRailPreview && !onExistingTrainPreview)
            {
                _previewController.SetActive(false);
            }
        }
        
        public void Disable()
        {
            _previewController.SetActive(false);
        }
        
        private bool PlaceTrainOnRail(PlaceSystemUpdateContext context)
        {
            if (!_detector.TryDetectOnRail(context.HoldingItemId, out var hit))
            {
                return false;
            }
            _previewController.SetActive(true);
            _previewController.ShowPreview(context.HoldingItemId, hit.PreviewPosition, hit.PreviewRotation, hit.IsPlaceable);
            
            if (!hit.IsPlaceable)
            {
                return true;
            }
            
            if (InputManager.Playable.ScreenLeftClick.GetKeyUp)
            {
                ClientContext.VanillaApi.SendOnly.PlaceTrainOnRail(hit.Specifier, context.CurrentSelectHotbarSlotIndex);
            }
            
            return true;
        }
        
        private bool PlaceTrainOnExistingTrain(PlaceSystemUpdateContext context)
        {
            if (!_detector.TryDetectOnExistingTrain(out var hit))
            {
                return false;
            }
            
            _previewController.SetActive(true);
            _previewController.ShowPreview(context.HoldingItemId, hit.PreviewPosition, hit.PreviewRotation, hit.IsPlaceable);
            
            if (!hit.IsPlaceable)
            {
                return true;
            }
            
            if (InputManager.Playable.ScreenLeftClick.GetKeyUp)
            {
                Debug.Log("Place train on existing train");
                Debug.Log($"{hit.Train.RailPosition.DistanceToNextNode} {hit.Train.RailPosition.TrainLength} {string.Join(", ", hit.Train.RailPosition.RailNodes.Select(r => r.OriginalPosition))}");
                ClientContext.VanillaApi.SendOnly.PlaceTrainOnExistingTrain(hit.Train.TrainCarId, hit.Train., context.CurrentSelectHotbarSlotIndex);
            }
            
            
            return true;
        }
    }
}