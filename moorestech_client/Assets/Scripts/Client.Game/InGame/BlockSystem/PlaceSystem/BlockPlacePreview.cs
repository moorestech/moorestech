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
        private GroundCollisionDetector[] _collisionDetectors;
        
        public bool IsActive => gameObject.activeSelf;
        
        public bool IsCollisionGround
        {
            get
            {
                if (_collisionDetectors == null) return false;
                return _collisionDetectors.Any(detector => detector.IsCollision);
            }
        }
        
        private void Start()
        {
            _blockPlacePreviewObjectPool = new BlockPlacePreviewObjectPool(transform);
        }
        
        public void SetPreview(bool placeable, List<PlaceInfo> currentPlaceInfos, BlockConfigData holdingBlockConfig)
        {
            CreatePreviewObjects(currentPlaceInfos, holdingBlockConfig);
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
        private void CreatePreviewObjects(List<PlaceInfo> placePointInfos, BlockConfigData blockConfig)
        {
            // さっきと違うブロックだったら削除する
            if (_previewBlockConfig == null || _previewBlockConfig.BlockId != blockConfig.BlockId)
            {
                _previewBlockConfig = blockConfig;
                _blockPlacePreviewObjectPool.AllDestroy();
            }
            
            _blockPlacePreviewObjectPool.AllUnUse();
            
            // プレビューブロックの位置を設定
            for (var i = 0; i < placePointInfos.Count; i++)
            {
                var placePoint = placePointInfos[i].Point;
                var direction = placePointInfos[i].Direction;
                var verticalDirection = placePointInfos[i].VerticalDirection;
                
                var blockId = blockConfig.BlockId;
                if (BlockVerticalConfig.BlockVerticalDictionary.TryGetValue((blockId, verticalDirection), out var verticalBlockId))
                {
                    blockId = verticalBlockId;
                }
                
                var pos = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(placePoint, direction, blockId);
                var rot = direction.GetRotation();
                
                var previewBlock = _blockPlacePreviewObjectPool.GetObject(blockId);
                previewBlock.transform.position = pos;
                previewBlock.transform.rotation = rot;
            }
        }
    }
}