using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
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
        
        public PlaceInfo ManualUpdate(ItemId itemId)
        {
            _previewBlockController.SetActive(false);
            
            if (!_isActive) return null;
            
            var holdingBlockMaster = MasterHolder.BlockMaster.GetBlockMaster(MasterHolder.BlockMaster.GetBlockId(itemId));
            if (!PlaceSystemUtil.TryGetRayHitBlockPosition(_mainCamera, HeightOffset, DefaultBlockDirection, holdingBlockMaster, out var placePoint, out var boundingBoxSurface)) return null;
            PlacePosition = placePoint;
            
            _previewBlockController.SetActive(true);
            
            RotationRailComponent();
            
            var placeInfo = CreatePlaceInfo();
            _previewBlockController.SetPreviewAndGroundDetect(new List<PlaceInfo> { placeInfo }, holdingBlockMaster);
            ConnectorPosition = GetConnectorPosition(holdingBlockMaster);
            
            return placeInfo;
            
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