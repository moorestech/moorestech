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
        private BlockGameObject _previewBlock;
        
        public void SetPreview(Vector2Int blockPosition, BlockDirection blockDirection,BlockConfigData blockConfig)
        {
            var (pos,rot,scale) = SlopeBlockPlaceSystem.GetSlopeBeltConveyorTransform(blockConfig.Type,blockPosition, blockDirection,blockConfig.BlockSize);
            
            if (!_previewBlock || _previewBlock.BlockConfig.BlockId != blockConfig.BlockId) //TODO さっきと同じブロックだったら置き換え
            {
                if (_previewBlock)
                    Destroy(_previewBlock.gameObject);
                _previewBlock = MoorestechContext.BlockGameObjectContainer.CreateBlock(blockConfig.BlockId, pos, rot, scale, transform, blockPosition);
            }
            
            transform.position = pos;
            _previewBlock.transform.localPosition = Vector3.zero;
            _previewBlock.transform.rotation = rot;
            _previewBlock.transform.localScale = scale;
        }

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
    }
}