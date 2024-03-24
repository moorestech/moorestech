using Client.Game.Block;
using Client.Game.Context;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using UnityEngine;

namespace Client.Game.BlockSystem
{
    public class BlockPlacePreview : MonoBehaviour, IBlockPlacePreview
    {
        private BlockPreviewObject _previewBlock;

        public void SetPreview(Vector3Int blockPosition, BlockDirection blockDirection, BlockConfigData blockConfig)
        {
            var pos = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(blockPosition, blockDirection, blockConfig.BlockId);
            var rot = blockDirection.GetRotation();

            if (!_previewBlock || _previewBlock.BlockConfig.BlockId != blockConfig.BlockId) //TODO さっきと同じブロックだったら置き換え
            {
                if (_previewBlock)
                    Destroy(_previewBlock.gameObject);

                //プレビューブロックを設置
                _previewBlock = MoorestechContext.BlockGameObjectContainer.CreatePreviewBlock(blockConfig.BlockId);
                _previewBlock.transform.SetParent(transform);
                _previewBlock.transform.localPosition = Vector3.zero;
                //プレビューブロックのマテリアルを変更
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