using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using Client.Input;
using Cysharp.Threading.Tasks;
using System.Threading;
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
                RequestPlacementAsync(hit, context.CurrentSelectHotbarSlotIndex).Forget();
            }

            #region Internal

            async UniTaskVoid RequestPlacementAsync(TrainCarPlacementHit placementHit, int hotBarSlot)
            {
                // 設置レスポンスを待機する
                // Await placement response
                var response = await ClientContext.VanillaApi.Response.PlaceTrainOnRail(placementHit.Specifier, placementHit.RailPosition, hotBarSlot, CancellationToken.None);
                if (response == null || !response.Success)
                {
                    Debug.LogWarning($"[TrainCarPlaceSystem] PlaceTrain failed. reason={response?.FailureType}");
                }
            }

            #endregion
        }

        public void Disable()
        {
            _previewController.SetActive(false);
        }
    }
}
