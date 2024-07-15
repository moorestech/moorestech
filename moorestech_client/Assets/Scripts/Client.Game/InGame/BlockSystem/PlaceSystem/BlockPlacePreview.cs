using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public class BlockPlacePreview : MonoBehaviour, IBlockPlacePreview
    {
        private BlockConfigData _previewBlockConfig;
        private BlockPlacePreviewObjectPool _blockPlacePreviewObjectPool;
        
        public bool IsActive => gameObject.activeSelf;
        
        
        private void Start()
        {
            _blockPlacePreviewObjectPool = new BlockPlacePreviewObjectPool(transform);
        }
        
        public List<bool> SetPreviewAndGroundDetect(List<PlaceInfo> placePointInfos, BlockConfigData blockConfig)
        {
            // さっきと違うブロックだったら削除する
            if (_previewBlockConfig == null || _previewBlockConfig.BlockId != blockConfig.BlockId)
            {
                _previewBlockConfig = blockConfig;
                _blockPlacePreviewObjectPool.AllDestroy();
            }
            
            _blockPlacePreviewObjectPool.AllUnUse();
            
            // プレビューブロックの位置を設定
            var isGroundDetectedList = new List<bool>();
            foreach (var placeInfo in placePointInfos)
            {
                var verticalDirection = placeInfo.VerticalDirection;
                
                var blockId = blockConfig.BlockId;
                if (BlockVerticalConfig.BlockVerticalDictionary.TryGetValue((blockId, verticalDirection), out var verticalBlockId))
                {
                    blockId = verticalBlockId;
                }
                
                var pos = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(placeInfo.Point, placeInfo.Direction, blockId);
                var rot = placeInfo.Direction.GetRotation();
                
                var previewBlock = _blockPlacePreviewObjectPool.GetObject(blockId);
                previewBlock.SetTransform(pos,rot);
                var isGroundDetected = previewBlock.IsCollisionGround;
                isGroundDetectedList.Add(isGroundDetected);
                
                previewBlock.SetPlaceableColor(!isGroundDetected && placeInfo.Placeable);
            }
            
            return isGroundDetectedList;
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
    }
}