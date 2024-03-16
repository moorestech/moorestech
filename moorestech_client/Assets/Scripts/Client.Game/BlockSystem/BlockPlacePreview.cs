using Client.Game.Block;
using Client.Game.Context;
using Game.World.Interface.DataStore;
using Constant;
using Game.Block.Config;
using Game.Block.Interface.BlockConfig;
using MainGame.ModLoader.Glb;
using UnityEngine;

namespace MainGame.UnityView.Block
{
    public class BlockPlacePreview : MonoBehaviour, IBlockPlacePreview
    {
        private BlockPreviewObject _previewBlock;
        
        public void SetPreview(Vector3Int blockPosition, BlockDirection blockDirection,BlockConfigData blockConfig)
        {
            var pos = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(blockPosition, blockDirection, blockConfig.BlockId);
            var rot = blockDirection.GetRotation();
            
            if (!_previewBlock || _previewBlock.BlockConfig.BlockId != blockConfig.BlockId) //TODO さっきと同じブロックだったら置き換え
            {
                if (_previewBlock)
                    Destroy(_previewBlock.gameObject);

                _previewBlock = MoorestechContext.BlockGameObjectContainer.CreatePreviewBlock(blockConfig.BlockId);
                _previewBlock.transform.SetParent(transform);
                _previewBlock.transform.localPosition = Vector3.zero;
            }
            
            transform.position = pos;
            _previewBlock.transform.rotation = rot;
        }

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
    }
}