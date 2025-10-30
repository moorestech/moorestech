using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
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
            
            if (!TryGetRayHitBlockPosition(_mainCamera, HeightOffset, DefaultBlockDirection, out var placePoint, out var boundingBoxSurface)) return;
            
            _previewBlockController.SetActive(true);
            
            var placeInfo = new List<PreviewPlaceInfo>
            {
                new PreviewPlaceInfo(new PlaceInfo
                {
                    Position = placePoint,
                    Direction = DefaultBlockDirection,
                    VerticalDirection = BlockVerticalDirection.Horizontal,
                    Placeable = true
                }),
            };
            _previewBlockController.SetPreviewAndGroundDetect(placeInfo, boundingBoxSurface.BlockGameObject.BlockMasterElement);
        }
        
        public void Disable()
        {
        }
    }
}