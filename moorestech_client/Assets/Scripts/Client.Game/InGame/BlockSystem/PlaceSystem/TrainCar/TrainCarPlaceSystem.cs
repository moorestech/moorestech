using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    public class TrainCarPlaceSystem : IPlaceSystem
    {
        private readonly ITrainCarPlacementDetector _detector;
        private readonly ITrainCarPreviewController _previewController;
        private readonly ITrainCarPlacementInput _input;
        private readonly ITrainCarPlacementSender _sender;

        private ItemId _currentItemId = ItemMaster.EmptyItemId;

        public TrainCarPlaceSystem(
            ITrainCarPlacementDetector detector,
            ITrainCarPreviewController previewController,
            ITrainCarPlacementInput input,
            ITrainCarPlacementSender sender)
        {
            _detector = detector;
            _previewController = previewController;
            _input = input;
            _sender = sender;
        }

        public void Enable()
        {
        }

        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            if (context.IsSelectSlotChanged)
            {
                _currentItemId = context.HoldingItemId;
                if (_currentItemId != ItemMaster.EmptyItemId)
                {
                    _previewController.Initialize(_currentItemId);
                }
                else
                {
                    _previewController.HidePreview();
                }
            }

            if (context.HoldingItemId == ItemMaster.EmptyItemId)
            {
                _previewController.HidePreview();
                return;
            }

            if (!_detector.TryDetect(context.HoldingItemId, out var hit))
            {
                _previewController.HidePreview();
                return;
            }

            _previewController.ShowPreview(hit.PreviewPosition, hit.PreviewRotation, hit.IsPlaceable);

            if (!hit.IsPlaceable)
            {
                return;
            }

            if (_input.IsPlaceTriggered())
            {
                _sender.Send(hit.Specifier, context.CurrentSelectHotbarSlotIndex);
                _previewController.HidePreview();
            }
        }

        public void Disable()
        {
            _previewController.HidePreview();
            _currentItemId = ItemMaster.EmptyItemId;
        }
    }
}