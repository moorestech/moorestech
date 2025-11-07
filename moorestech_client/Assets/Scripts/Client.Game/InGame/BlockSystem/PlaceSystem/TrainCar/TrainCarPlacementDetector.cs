using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Train;
using Core.Master;
using UnityEngine;
using static Server.Protocol.PacketResponse.RailConnectionEditProtocol;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    public readonly struct TrainCarPlacementHit
    {
        public TrainCarPlacementHit(RailComponentSpecifier specifier, Vector3 previewPosition, Quaternion previewRotation, bool isPlaceable)
        {
            Specifier = specifier;
            PreviewPosition = previewPosition;
            PreviewRotation = previewRotation;
            IsPlaceable = isPlaceable;
        }
        
        public RailComponentSpecifier Specifier { get; }
        public Vector3 PreviewPosition { get; }
        public Quaternion PreviewRotation { get; }
        public bool IsPlaceable { get; }
    }
    
    public interface ITrainCarPlacementDetector
    {
        bool TryDetect(ItemId holdingItemId, out TrainCarPlacementHit hit);
    }
    
    public class TrainCarPlacementDetector : ITrainCarPlacementDetector
    {
        private readonly Camera _mainCamera;
        
        public TrainCarPlacementDetector(Camera mainCamera)
        {
            _mainCamera = mainCamera;
        }

        public bool TryDetect(ItemId holdingItemId, out TrainCarPlacementHit hit)
        {
            hit = default;

            if (!PlaceSystemUtil.TryGetRaySpecifiedComponentHit<RailSplineComponent>(_mainCamera, out var rail, LayerConst.BlockOnlyLayerMask))
            {
                return false;
            }
            
            var block = rail.StartBlock;
            var railSpecifier = RailComponentSpecifier.CreateRailSpecifier(block.BlockPosInfo.OriginalPos);
            var previewPosition = block.BlockPosInfo.OriginalPos + new Vector3(0.5f, 0.5f, 0.5f);
            var previewRotation = Quaternion.identity;

            hit = new TrainCarPlacementHit(railSpecifier, previewPosition, previewRotation, true);
            return true;
        }
    }
}

