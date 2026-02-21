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
            _detector.ResetSelection();
            _previewController.SetActive(true);
        }

        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            // スロット変更時は候補選択を初期化する
            // Reset route selection when slot selection changes
            if (context.IsSelectSlotChanged)
            {
                _detector.ResetSelection();
            }

            // Rキーで「反転優先」の順序で次候補へ進める
            // Advance to the next candidate in reverse-priority order on R key
            // TODO InputManager整備
            if (InputManager.Playable.BlockPlaceRotation.GetKeyDown)
            {
                _detector.AdvanceSelection();
            }

            // レール上の設置候補を検出する
            // Detect placement candidate on the rail
            if (!_detector.TryDetect(context.HoldingItemId, out var hit))
            {
                _previewController.SetActive(false);
                return;
            }

            // プレビュー表示可否と描画状態を更新する
            // Update preview visibility and rendering
            var railPosition = hit.RailPosition;
            var hasPreview = railPosition != null && _previewController.ShowPreview(context.HoldingItemId, railPosition, hit.IsPlaceable);
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
                // レスポンスを待機して結果を検証する
                // Await placement response and validate result
                var response = await ClientContext.VanillaApi.Response.PlaceTrainOnRail(placementHit.RailPosition, hotBarSlot, CancellationToken.None);
                if (response == null || !response.Success)
                {
                    Debug.LogWarning($"[TrainCarPlaceSystem] PlaceTrain failed. reason={response?.FailureType}");
                }
            }

            #endregion
        }

        public void Disable()
        {
            _detector.ResetSelection();
            _previewController.SetActive(false);
        }
    }
}
