using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Input;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;
using static Client.Game.InGame.BlockSystem.PlaceSystem.Util.PlaceSystemUtil;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail
{
    public class TrainRailPlaceSystem : IPlaceSystem
    {
        private const int HeightOffset = 0;
        private const BlockDirection DefaultBlockDirection = BlockDirection.North;
        
        private readonly Camera _mainCamera;
        private readonly IPlacementPreviewBlockGameObjectController _previewBlockController;
        
        private RailComponentDirection _railDirection;
        
        public TrainRailPlaceSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController)
        {
            _mainCamera = mainCamera;
            _previewBlockController = previewBlockController;
        }
        
        public void Enable()
        {
        }
        
        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            _previewBlockController.SetActive(false);
            
            var holdingBlockMaster = MasterHolder.BlockMaster.GetBlockMaster(context.HoldingItemId);
            if (!TryGetRayHitBlockPosition(_mainCamera, HeightOffset, DefaultBlockDirection, holdingBlockMaster, out var placePoint, out var boundingBoxSurface)) return;
            
            _previewBlockController.SetActive(true);
            
            RotationRailComponent();
            
            var placeInfo = CreatePlaceInfo();
            _previewBlockController.SetPreviewAndGroundDetect(placeInfo, holdingBlockMaster);
            PlaceBlock(placeInfo);
            
            #region Internal
            
            void RotationRailComponent()
            {
                if (!InputManager.Playable.BlockPlaceRotation.GetKeyDown) return;
                
                var nextDirection = (int) _railDirection + 1;
                if (nextDirection > (int) RailComponentDirection.Direction315)
                {
                    nextDirection = (int) RailComponentDirection.Direction0;
                }
                _railDirection = (RailComponentDirection) nextDirection;
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
                        new(RailBridgePierComponentStateDetail.StateDetailKey, MessagePack.MessagePackSerializer.Serialize(new RailBridgePierComponentStateDetail(_railDirection.ToVector3()))),
                    }
                };
                return new List<PlaceInfo> {info};
            }
            
            
            void PlaceBlock(List<PlaceInfo>  info)
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyUp) return;
                
                SendPlaceProtocol(info, context);
            }
            
            #endregion
        }
        
        public void Disable()
        {
            _previewBlockController.SetActive(false);
        }
    }
}