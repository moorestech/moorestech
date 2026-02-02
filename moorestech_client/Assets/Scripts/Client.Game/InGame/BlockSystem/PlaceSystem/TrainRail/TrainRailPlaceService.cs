using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Input;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using MessagePack;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail
{
    public class TrainRailPlaceSystemService
    {
        private const int HeightOffset = 0;
        private const BlockDirection DefaultBlockDirection = BlockDirection.North;
        public RailComponentDirection RailDirection { get; private set; }
        
        private readonly Camera _mainCamera;
        private readonly IPlacementPreviewBlockGameObjectController _previewBlockController;
        
        public TrainRailPlaceSystemService(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController)
        {
            _mainCamera = mainCamera;
            _previewBlockController = previewBlockController;
        }
        
        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            _previewBlockController.SetActive(false);
            
            var holdingBlockMaster = MasterHolder.BlockMaster.GetBlockMaster(context.HoldingItemId);
            if (!PlaceSystemUtil.TryGetRayHitBlockPosition(_mainCamera, HeightOffset, DefaultBlockDirection, holdingBlockMaster, out var placePoint, out var boundingBoxSurface)) return;
            
            _previewBlockController.SetActive(true);
            
            RotationRailComponent();
            
            List<PlaceInfo> placeInfo = CreatePlaceInfo();
            _previewBlockController.SetPreviewAndGroundDetect(placeInfo, holdingBlockMaster);
            PlaceBlock(placeInfo);
            
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
            
            List<PlaceInfo> CreatePlaceInfo()
            {
                var info = new PlaceInfo
                {
                    Position = placePoint,
                    Direction = DefaultBlockDirection,
                    VerticalDirection = BlockVerticalDirection.Horizontal,
                    Placeable = true,
                    CreateParams = new BlockCreateParam[]
                    {
                        new(RailBridgePierComponentStateDetail.StateDetailKey, MessagePackSerializer.Serialize(new RailBridgePierComponentStateDetail(RailDirection.ToVector3()))),
                    },
                };
                return new List<PlaceInfo> { info };
            }
            
            
            void PlaceBlock(List<PlaceInfo> info)
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyUp) return;
                
                PlaceSystemUtil.SendPlaceProtocol(info, context);
            }
            
            #endregion
        }
        
        public void Disable()
        {
            _previewBlockController.SetActive(false);
        }
    }
}