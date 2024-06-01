using System.Linq;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem
{
    public class BlockPlacePreview : MonoBehaviour, IBlockPlacePreview
    {
        private GroundCollisionDetector[] _collisionDetectors;
        private BlockPreviewObject _previewBlock;
        
        public bool IsActive => gameObject.activeSelf;
        
        public bool IsCollisionGround
        {
            get
            {
                if (_collisionDetectors == null) return false;
                
                return _collisionDetectors.Any(detector => detector.IsCollision);
            }
        }
        
        public void SetPreview(bool placeable, Vector3Int blockPosition, BlockDirection blockDirection, BlockConfigData blockConfig)
        {
            SetPreview(blockPosition, blockDirection, blockConfig);
            
            var materialPath = placeable ? MaterialConst.PreviewPlaceBlockMaterial : MaterialConst.PreviewNotPlaceableBlockMaterial;
            SetMaterial(Resources.Load<Material>(materialPath));
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
        private void SetPreview(Vector3Int blockPosition, BlockDirection blockDirection, BlockConfigData blockConfig)
        {
            var pos = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(blockPosition, blockDirection, blockConfig.BlockId);
            var rot = blockDirection.GetRotation();
            
            if (!_previewBlock || _previewBlock.BlockConfig.BlockId != blockConfig.BlockId) //TODO さっきと同じブロックだったら置き換え
            {
                if (_previewBlock)
                    Destroy(_previewBlock.gameObject);
                
                //プレビューブロックを設置
                _previewBlock = ClientContext.BlockGameObjectContainer.CreatePreviewBlock(blockConfig.BlockId);
                _previewBlock.transform.SetParent(transform);
                _previewBlock.transform.localPosition = Vector3.zero;
                _collisionDetectors = _previewBlock.GetComponentsInChildren<GroundCollisionDetector>();
            }
            
            transform.position = pos;
            _previewBlock.transform.rotation = rot;
        }
        
        public void SetMaterial(Material material)
        {
            //プレビューブロックのマテリアルを変更
            _previewBlock.SetMaterial(material);
        }
    }
}