using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View.Object.Core;
using Client.Game.InGame.Train.View.Object.Material;
using Client.Input;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    public class TrainCarPlaceSystem : PlaceSystemBase<TrainCarPlacementTarget>
    {
        private readonly ITrainCarPlacementDetector _detector;
        private readonly TrainCarPreviewController _previewController;
        private readonly TrainCarObjectDatastore _trainCarObjectDatastore;
        private readonly TrainUnitClientCache _trainUnitClientCache;

        public TrainCarPlaceSystem(
            ITrainCarPlacementDetector detector,
            TrainCarPreviewController previewController,
            TrainCarObjectDatastore trainCarObjectDatastore,
            TrainUnitClientCache trainUnitClientCache)
        {
            _detector = detector;
            _previewController = previewController;
            _trainCarObjectDatastore = trainCarObjectDatastore;
            _trainUnitClientCache = trainUnitClientCache;
        }

        public override void Enable()
        {
            _detector.ResetSelection();
            _previewController.SetActive(true);
        }

        protected override void ManualUpdate(TrainCarPlacementTarget target, bool isSelectionChanged)
        {
            // 選択変更時は候補選択を初期化する
            // Reset route selection when the build-menu selection changes
            if (isSelectionChanged)
            {
                _detector.ResetSelection();
            }

            // Rキーで候補順を進める
            // Advance to the next placement candidate on the R key
            if (InputManager.Playable.BlockPlaceRotation.GetKeyDown)
            {
                _detector.AdvanceSelection();
            }

            // レール上の設置候補を検出する
            // Detect the placement candidate on the rail
            if (!_detector.TryDetect(target.TrainCarGuid, out var hit))
            {
                _previewController.SetActive(false);
                return;
            }

            // 既存列車へのスナップ対象はこのフレームだけハイライトを要求する
            // Request current-frame highlight for existing trains that are snap targets
            RequestPlacementOverlapHighlight(hit.OverlapTrainUnitInstanceIds);

            // railpositionからpreviewを描画する
            // Render the preview directly from railposition
            var railPosition = hit.RailPosition;
            var hasPreview = railPosition != null && _previewController.ShowPreview(target.TrainCarGuid, railPosition, hit.IsPlaceable);
            _previewController.SetActive(hasPreview);
            if (!hit.IsPlaceable)
            {
                return;
            }

            // クリック時に選択中の車両Guidで設置リクエストを送る
            // Send the placement request with the selected car guid on click
            if (InputManager.Playable.ScreenLeftClick.GetKeyUp)
            {
                RequestPlacementAsync(hit, target.TrainCarGuid).Forget();
            }

            #region Internal

            async UniTaskVoid RequestPlacementAsync(TrainCarPlacementHit placementHit, Guid trainCarGuid)
            {
                // 既存編成への連結modeでは対象unitを明示して送る
                // In attach mode, send the target unit explicitly
                if (placementHit.PlacementMode == TrainCarPlacementMode.AttachToExistingTrainUnit)
                {
                    if (placementHit.TargetTrainUnitInstanceId == TrainUnitInstanceId.Empty)
                    {
                        Debug.LogWarning("[TrainCarPlaceSystem] AttachTrainCar failed. reason=InvalidTargetTrainUnitInstanceId");
                        return;
                    }

                    var attachResponse = await ClientContext.VanillaApi.Response.AttachTrainCarToUnit(
                        placementHit.TargetTrainUnitInstanceId,
                        placementHit.RailPosition,
                        trainCarGuid,
                        placementHit.AttachCarFacingForward,
                        placementHit.AttachTargetEndpoint == TrainCarAttachTargetEndpoint.Head,
                        CancellationToken.None);
                    if (attachResponse == null || !attachResponse.Success)
                    {
                        Debug.LogWarning($"[TrainCarPlaceSystem] AttachTrainCar failed. reason={attachResponse?.FailureType}");
                    }
                    return;
                }

                // 新規編成modeではRailPositionのみで設置を依頼する
                // In new-unit mode, request placement with only the RailPosition
                var placeResponse = await ClientContext.VanillaApi.Response.PlaceTrainOnRail(placementHit.RailPosition, trainCarGuid, CancellationToken.None);
                if (placeResponse == null || !placeResponse.Success)
                {
                    Debug.LogWarning($"[TrainCarPlaceSystem] PlaceTrain failed. reason={placeResponse?.FailureType}");
                }
            }

            #endregion
        }

        public override void Disable()
        {
            _detector.ResetSelection();
            _previewController.SetActive(false);
        }

        private void RequestPlacementOverlapHighlight(IReadOnlyCollection<TrainUnitInstanceId> overlapTrainUnitIds)
        {
            if (overlapTrainUnitIds == null || overlapTrainUnitIds.Count == 0)
            {
                return;
            }

            // overlapしたunit配下のcarへ直接overlayを要求する
            // Request overlays directly for cars under overlapped units
            foreach (var overlapTrainUnitId in overlapTrainUnitIds)
            {
                if (!_trainUnitClientCache.TryGet(overlapTrainUnitId, out var trainUnit))
                {
                    continue;
                }

                var cars = trainUnit.Cars;
                for (var i = 0; i < cars.Count; i++)
                {
                    if (!_trainCarObjectDatastore.TryGetEntity(cars[i].TrainCarInstanceId, out var entity))
                    {
                        continue;
                    }

                    entity.RequestOverlayForCurrentFrame(TrainCarVisualMaterialMode.PlacementOverlapHighlight);
                }
            }
        }
    }
}
