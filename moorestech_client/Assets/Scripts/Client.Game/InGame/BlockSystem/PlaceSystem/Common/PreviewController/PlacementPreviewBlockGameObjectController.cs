using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController
{
    public class PlacementPreviewBlockGameObjectController : MonoBehaviour, IPlacementPreviewBlockGameObjectController
    {
        private BlockMasterElement _previewBlockMasterElement;
        private BlockPlacePreviewObjectPool _blockPlacePreviewObjectPool;
        
        public bool IsActive => gameObject.activeSelf;
        
        
        private void Awake()
        {
            _blockPlacePreviewObjectPool = new BlockPlacePreviewObjectPool(transform);
            SetActive(false);
        }
        
        public List<bool> SetPreviewAndGroundDetect(List<PreviewPlaceInfo> placePointInfos, BlockMasterElement holdingBlockMaster)
        {
            // さっきと違うブロックだったら削除する
            if (_previewBlockMasterElement == null || _previewBlockMasterElement.BlockGuid != holdingBlockMaster.BlockGuid)
            {
                _previewBlockMasterElement = holdingBlockMaster;
                _blockPlacePreviewObjectPool.AllDestroy();
            }
            
            _blockPlacePreviewObjectPool.AllUnUse();
            
            // プレビューブロックの位置を設定
            var isGroundDetectedList = new List<bool>();
            foreach (var previewPlaceInfo in placePointInfos)
            {
                var placeInfo = previewPlaceInfo.PlaceInfo;
                var blockId = holdingBlockMaster.BlockGuid.GetVerticalOverrideBlockId(placeInfo.VerticalDirection);
                
                var pos = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(placeInfo.Position, placeInfo.Direction, blockId);
                var rot = placeInfo.Direction.GetRotation();
                
                var previewBlock = _blockPlacePreviewObjectPool.GetObject(blockId);
                previewBlock.SetTransform(pos,rot);
                var isGroundDetected = previewBlock.IsCollisionGround;
                isGroundDetectedList.Add(isGroundDetected);
                
                previewBlock.SetPlaceableColor(!isGroundDetected && placeInfo.Placeable);
                previewBlock.SetPreviewStateDetail(previewPlaceInfo);
            }
            
            return isGroundDetectedList;
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
    }
}