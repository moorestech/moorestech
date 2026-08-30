using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Input;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail
{
    public class TrainRailPlaceSystemService
    {
        private const int HeightOffset = 0;
        private const BlockDirection DefaultBlockDirection = BlockDirection.North;
        public RailComponentDirection RailDirection { get; private set; }
        public Vector3Int PlacePosition { get; private set; }
        public Vector3 ConnectorPosition { get; private set; }
        
        private readonly Camera _mainCamera;
        private readonly IPlacementPreviewBlockGameObjectController _previewBlockController;
        private bool _isActive;
        
        public TrainRailPlaceSystemService(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController)
        {
            _mainCamera = mainCamera;
            _previewBlockController = previewBlockController;
        }
        
        public PlaceInfo ManualUpdate(BlockId blockId, PlacementFeedback feedback)
        {
            _previewBlockController.SetActive(false);

            if (!_isActive) return null;

            var holdingBlockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            if (!PlaceSystemUtil.TryGetRayHitBlockPosition(_mainCamera, HeightOffset, DefaultBlockDirection, holdingBlockMaster, out var placePoint, out _)) return null;

            // 距離外なら理由のみ出しプレビュー無し
            // Beyond range, show only the reason and no preview
            if (!PlaceSystemUtil.IsPlaceableFromPlayer(placePoint)) { feedback.AddTooFar(); return null; }

            PlacePosition = placePoint;
            
            _previewBlockController.SetActive(true);
            
            RotationRailComponent();
            
            var placeInfos = new List<PlaceInfo> { CreatePlaceInfo() };
            _previewBlockController.SetPreview(placeInfos, holdingBlockMaster);
            var groundOverlaps = _previewBlockController.DetectGroundOverlaps();

            // 地面に埋まるセルを設置不可にし、その理由を積む。レール1セルは共有原因を判定しないためNone列を渡す
            // Mark ground-buried cells unplaceable and report that reason; the single rail cell judges no shared cause, so a None column is passed
            PlacementCellReasonReporter.ApplyGroundOverlapsAndReport(placeInfos, new[] { PlacementBlockCause.None }, placePoint, groundOverlaps, feedback);

            // 最終的なPlaceable状態でプレビュー色を更新
            // Update preview colors based on the final Placeable state
            _previewBlockController.UpdatePlaceableColors(placeInfos);

            ConnectorPosition = GetConnectorPosition(holdingBlockMaster);
            
            return placeInfos[0];
            
            #region Internal
            
            void RotationRailComponent()
            {
                if (!InputManager.Playable.BlockPlaceRotation.GetKeyDown) return;
                
                var nextDirection = (int)RailDirection + 1;
                if (nextDirection > (int)RailComponentDirection.Direction315)
                {
                    nextDirection = (int)RailComponentDirection.Direction0;
                }
                RailDirection = (RailComponentDirection)nextDirection;
            }
            
            PlaceInfo CreatePlaceInfo()
            {
                var info = new PlaceInfo
                {
                    Position = placePoint,
                    Direction = DefaultBlockDirection,
                    VerticalDirection = BlockVerticalDirection.Horizontal,
                    BlockId = blockId,
                    Placeable = true,
                    CreateParams = new BlockCreateParam[]
                    {
                        new(RailBridgePierComponentStateDetail.StateDetailKey, MessagePackSerializer.Serialize(new RailBridgePierComponentStateDetail(RailDirection.ToVector3()))),
                    },
                };
                
                return info;
            }
            
            Vector3 GetConnectorPosition(BlockMasterElement element)
            {
                var trainRailBlockParam = element.BlockParam as TrainRailBlockParam;
                return PlacePosition + trainRailBlockParam?.RailPosition ?? Vector3.zero;
            }
            
            #endregion
        }
        
        public void Enable()
        {
            _isActive = true;
        }
        
        public void Disable()
        {
            _previewBlockController.SetActive(false);
            _isActive = false;
        }
    }
}
