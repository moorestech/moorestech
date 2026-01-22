using Client.Game.InGame.Context;
using Client.Game.InGame.Train.RailGraph;
using Client.Input;
using Cysharp.Threading.Tasks;
using Game.Train.RailPosition;
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
            // レール上の設置候補を検出する
            // Detect placement candidate on the rail
            if (!_detector.TryDetect(context.HoldingItemId, out var hit))
            {
                _previewController.SetActive(false);
                return;
            }
            // プレビューの表示可否と描画を更新する
            // Update preview visibility and rendering
            var hasRailPosition = TryRestoreRailPosition(hit.RailPosition, out var railPosition);
            var hasPreview = hasRailPosition && _previewController.ShowPreview(context.HoldingItemId, railPosition, hit.IsPlaceable);
            _previewController.SetActive(hasPreview);

            if (!hit.IsPlaceable)
            {
                return;
            }

            // クリックで設置リクエストを送信する
            // Send placement request on click
            if (InputManager.Playable.ScreenLeftClick.GetKeyUp)
            {
                RequestPlacementAsync(hit, context.CurrentSelectHotbarSlotIndex).Forget();
            }

            #region Internal

            async UniTaskVoid RequestPlacementAsync(TrainCarPlacementHit placementHit, int hotBarSlot)
            {
                // レスポンスを待機する
                // Await placement response
                var response = await ClientContext.VanillaApi.Response.PlaceTrainOnRail(placementHit.RailPosition, hotBarSlot, CancellationToken.None);
                if (response == null || !response.Success)
                {
                    Debug.LogWarning($"[TrainCarPlaceSystem] PlaceTrain failed. reason={response?.FailureType}");
                }
            }

            bool TryRestoreRailPosition(RailPositionSaveData railPositionSaveData, out RailPosition railPosition)
            {
                // レールスナップショットから復元する
                // Restore RailPosition from snapshot
                railPosition = null;
                if (railPositionSaveData == null)
                {
                    return false;
                }
                railPosition = RailPositionFactory.Restore(railPositionSaveData, _cache);
                return railPosition != null;
            }

            #endregion
        }

        public void Disable()
        {
            _previewController.SetActive(false);
        }
    }
}
