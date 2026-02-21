using Client.Game.InGame.Context;
using Client.Game.InGame.Train.View.Object;
using Client.Input;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using System.Threading;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    public class TrainCarPlaceSystem : IPlaceSystem
    {
        private readonly ITrainCarPlacementDetector _detector;
        private readonly TrainCarPreviewController _previewController;
        private readonly TrainCarObjectDatastore _trainCarObjectDatastore;

        public TrainCarPlaceSystem(ITrainCarPlacementDetector detector, TrainCarPreviewController previewController, TrainCarObjectDatastore trainCarObjectDatastore)
        {
            _detector = detector;
            _previewController = previewController;
            _trainCarObjectDatastore = trainCarObjectDatastore;
        }

        public void Enable()
        {
            _detector.ResetSelection();
            _previewController.SetActive(true);
            _trainCarObjectDatastore.ClearPlacementOverlapHighlight();
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
                _trainCarObjectDatastore.ClearPlacementOverlapHighlight();
                _previewController.SetActive(false);
                return;
            }

            // 要件1の重複対象TrainUnitを描画ハイライトする
            // Highlight overlapped train units resolved by requirement-1 overlap detection
            _trainCarObjectDatastore.SetPlacementOverlapHighlight(hit.OverlapTrainInstanceIds);

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
                if (placementHit.PlacementMode == TrainCarPlacementMode.AttachToExistingTrainUnit)
                {
                    if (placementHit.TargetTrainInstanceId == TrainInstanceId.Empty)
                    {
                        Debug.LogWarning("[TrainCarPlaceSystem] AttachTrainCar failed. reason=InvalidTargetTrainInstanceId");
                        return;
                    }

                    // 既存編成への連結モードで設置を要求する
                    // Request attach-mode placement to existing train unit
                    var attachResponse = await ClientContext.VanillaApi.Response.AttachTrainCarToUnit(
                        placementHit.TargetTrainInstanceId,
                        placementHit.RailPosition,
                        hotBarSlot,
                        placementHit.AttachCarFacingForward,
                        placementHit.AttachTargetEndpoint == TrainCarAttachTargetEndpoint.Head,
                        CancellationToken.None);
                    if (attachResponse == null || !attachResponse.Success)
                    {
                        Debug.LogWarning($"[TrainCarPlaceSystem] AttachTrainCar failed. reason={attachResponse?.FailureType}");
                    }
                    return;
                }

                // 新規編成作成モードで設置を要求する
                // Request placement in new-train creation mode
                var placeResponse = await ClientContext.VanillaApi.Response.PlaceTrainOnRail(placementHit.RailPosition, hotBarSlot, CancellationToken.None);
                if (placeResponse == null || !placeResponse.Success)
                {
                    Debug.LogWarning($"[TrainCarPlaceSystem] PlaceTrain failed. reason={placeResponse?.FailureType}");
                }
            }

            #endregion
        }

        public void Disable()
        {
            _detector.ResetSelection();
            _trainCarObjectDatastore.ClearPlacementOverlapHighlight();
            _previewController.SetActive(false);
        }
    }
}
