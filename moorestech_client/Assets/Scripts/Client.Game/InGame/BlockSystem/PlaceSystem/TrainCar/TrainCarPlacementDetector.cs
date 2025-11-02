using System;
using Client.Common;
using Client.Game.InGame.Block;
using Core.Master;
using UnityEngine;
using static Server.Protocol.PacketResponse.RailConnectionEditProtocol;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    public class TrainCarPlacementDetector : ITrainCarPlacementDetector
    {
        private readonly Camera _mainCamera;
        private readonly int _raycastMask;

        public TrainCarPlacementDetector(Camera mainCamera)
        {
            _mainCamera = mainCamera;
            _raycastMask = LayerConst.BlockOnlyLayerMask | LayerConst.BlockBoundingBoxOnlyLayerMask;
        }

        public bool TryDetect(ItemId holdingItemId, out TrainCarPlacementHit hit)
        {
            hit = default;

            if (_mainCamera == null)
            {
                return false;
            }

            if (!Physics.Raycast(_mainCamera.ScreenPointToRay(UnityEngine.Input.mousePosition), out var raycastHit, float.PositiveInfinity, _raycastMask))
            {
                return false;
            }

            var block = raycastHit.transform.GetComponentInParent<BlockGameObject>();
            if (block == null)
            {
                return false;
            }

            if (!string.Equals(block.BlockMasterElement.BlockType, "TrainRail", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var railSpecifier = RailComponentSpecifier.CreateRailSpecifier(block.BlockPosInfo.OriginalPos);
            var previewPosition = block.BlockPosInfo.OriginalPos + new Vector3(0.5f, 0.5f, 0.5f);
            var previewRotation = Quaternion.identity;

            hit = new TrainCarPlacementHit(railSpecifier, previewPosition, previewRotation, true);
            return true;
        }
    }
}

