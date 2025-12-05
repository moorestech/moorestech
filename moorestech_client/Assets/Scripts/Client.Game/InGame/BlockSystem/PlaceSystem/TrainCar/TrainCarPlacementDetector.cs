using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Entity.Object;
using Client.Game.InGame.Train;
using Core.Master;
using UnityEngine;
using static Server.Protocol.PacketResponse.RailConnectionEditProtocol;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    public readonly struct TrainCarRailPlacementHit
    {
        public TrainCarRailPlacementHit(RailComponentSpecifier specifier, Vector3 previewPosition, Quaternion previewRotation, bool isPlaceable)
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
    
    public readonly struct TrainCarExistingTrainPlacementHit
    {
        public TrainCarExistingTrainPlacementHit(TrainCarEntityObject train, bool isFront, Vector3 previewPosition, Quaternion previewRotation, bool isPlaceable)
        {
            Train = train;
            IsFront = isFront;
            PreviewPosition = previewPosition;
            PreviewRotation = previewRotation;
            IsPlaceable = isPlaceable;
        }
        
        public TrainCarEntityObject Train { get; }
        public bool IsFront { get; }
        public Vector3 PreviewPosition { get; }
        public Quaternion PreviewRotation { get; }
        public bool IsPlaceable { get; }
    }
    
    public interface ITrainCarPlacementDetector
    {
        bool TryDetectOnRail(ItemId holdingItemId, out TrainCarRailPlacementHit hit);
        bool TryDetectOnExistingTrain(out TrainCarExistingTrainPlacementHit hit);
    }
    
    public class TrainCarPlacementDetector : ITrainCarPlacementDetector
    {
        private readonly Camera _mainCamera;
        
        public TrainCarPlacementDetector(Camera mainCamera)
        {
            _mainCamera = mainCamera;
        }
        
        public bool TryDetectOnRail(ItemId holdingItemId, out TrainCarRailPlacementHit hit)
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
            
            hit = new TrainCarRailPlacementHit(railSpecifier, previewPosition, previewRotation, true);
            return true;
        }
        
        public bool TryDetectOnExistingTrain(out TrainCarExistingTrainPlacementHit hit)
        {
            hit = default;
            
            if (!PlaceSystemUtil.TryGetRaySpecifiedComponentHit<TrainCarEntityChildrenObject>(_mainCamera, out var otherTrain, LayerConst.BlockOnlyLayerMask))
            {
                return false;
            }
            
            var isFront = false;            // todo 一旦後方方向に追加するようにする
            var previewPosition = otherTrain.transform.position + Vector3.one * 2;
            var previewRotation = otherTrain.transform.rotation;
            var isPlaceable = true;
            
            hit = new TrainCarExistingTrainPlacementHit(otherTrain.TrainCarEntityObject, isFront, previewPosition, previewRotation, isPlaceable);
            return true;
        }
    }
}