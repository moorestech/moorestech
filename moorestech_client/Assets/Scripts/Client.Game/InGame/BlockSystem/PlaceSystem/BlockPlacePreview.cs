using System.Collections.Generic;
using System.Linq;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public class BlockPlacePreview : MonoBehaviour, IBlockPlacePreview
    {
        private readonly List<BlockPreviewObject> _previewBlocks = new();
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
        
        public void SetPreview(bool placeable, Vector3Int startPoint, Vector3Int endPoint, bool isStartZDirection, BlockDirection blockDirection, BlockConfigData blockConfig)
        {
            CreatePreviewObjects(startPoint, endPoint, isStartZDirection, blockDirection, blockConfig);
            
            var materialPath = placeable ? MaterialConst.PreviewPlaceBlockMaterial : MaterialConst.PreviewNotPlaceableBlockMaterial;
            //SetMaterial(Resources.Load<Material>(materialPath));
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
        private void CreatePreviewObjects(Vector3Int startPoint, Vector3Int endPoint, bool isStartZDirection, BlockDirection blockDirection, BlockConfigData blockConfig)
        {
            var placePoints = BlockPlacePointCalculator.CalculatePoint(startPoint, endPoint, isStartZDirection);
            
            // さっきと違うブロックだったら削除する
            if (_previewBlocks.Count != 0 && _previewBlocks[0].BlockConfig.BlockId != blockConfig.BlockId)
            {
                foreach (var previewBlock in _previewBlocks)
                {
                    Destroy(previewBlock.gameObject);
                }
                
                _previewBlocks.Clear();
            }
            
            if (_previewBlocks.Count < placePoints.Count)
            {
                // 必要分なければ作成する
                for (var i = _previewBlocks.Count; i < placePoints.Count; i++)
                {
                    var previewBlock = CreatePreviewObject(blockConfig);
                    _previewBlocks.Add(previewBlock);
                }
            }
            else
            {
                // 余分なものを非表示にする
                for (var i = placePoints.Count; i < _previewBlocks.Count; i++)
                {
                    _previewBlocks[i].SetActive(false);
                }
            }
            
            // プレビューブロックの位置を設定
            for (var i = 0; i < placePoints.Count; i++)
            {
                var placePoint = placePoints[i];
                
                var pos = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(placePoint, blockDirection, blockConfig.BlockId);
                var rot = blockDirection.GetRotation();
                
                _previewBlocks[i].transform.position = pos;
                _previewBlocks[i].transform.rotation = rot;
                _previewBlocks[i].SetActive(true);
            }
        }
        
        private BlockPreviewObject CreatePreviewObject(BlockConfigData blockConfig)
        {
            var previewBlock = ClientContext.BlockGameObjectContainer.CreatePreviewBlock(blockConfig.BlockId);
            previewBlock.transform.SetParent(transform);
            previewBlock.transform.localPosition = Vector3.zero;
            _collisionDetectors = previewBlock.GetComponentsInChildren<GroundCollisionDetector>();
            
            return previewBlock;
        }
        
        public void SetMaterial(Material material)
        {
            //プレビューブロックのマテリアルを変更
            foreach (var previewBlock in _previewBlocks)
            {
                previewBlock.SetMaterial(material);
            }
        }
    }
}