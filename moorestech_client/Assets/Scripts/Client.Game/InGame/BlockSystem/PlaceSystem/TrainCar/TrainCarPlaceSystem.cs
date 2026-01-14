using Client.Game.InGame.Context;
using Client.Game.InGame.Train;
using Client.Input;
using Cysharp.Threading.Tasks;
using Game.Train.RailGraph;
using Game.Train.Train;
using System.Threading;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    public class TrainCarPlaceSystem : IPlaceSystem
    {
        private readonly ITrainCarPlacementDetector _detector;
        private readonly TrainCarPreviewController _previewController;
        private readonly RailGraphClientCache _cache;
        
        public TrainCarPlaceSystem(ITrainCarPlacementDetector detector, TrainCarPreviewController previewController, RailGraphClientCache cache)
        {
            _detector = detector;
            _previewController = previewController;
            _cache = cache;
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
            var hasPreview = TryResolvePreviewPose(hit.RailPosition);
            _previewController.SetActive(hasPreview);
            if (hasPreview)
            {
                _previewController.ShowPreview(context.HoldingItemId, hit.IsPlaceable);
            }

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
                var response = await ClientContext.VanillaApi.Response.PlaceTrainOnRail(placementHit.RailPosition, hotBarSlot, CancellationToken.None);
                if (response == null || !response.Success)
                {
                    Debug.LogWarning($"[TrainCarPlaceSystem] PlaceTrain failed. reason={response?.FailureType}");
                }
            }

            bool TryResolvePreviewPose(RailPositionSaveData railPositionSaveData)
            {
                // レールスナップショットから位置と向きを復元する
                // Restore pose from the rail snapshot
                if (railPositionSaveData == null)
                {
                    return false;
                }
                var railPosition = RailPositionFactory.Restore(railPositionSaveData, _cache);
                if (railPosition == null)
                {
                    return false;
                }
                if (!TrainCarPoseCalculator.TryGetPose(railPosition, 0, out var position, out var forward))
                {
                    return false;
                }
                return true;
            }

            #endregion
        }

        public void Disable()
        {
            _previewController.SetActive(false);
        }
    }
}
