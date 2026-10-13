using System;
using System.Collections.Generic;

namespace Client.Game.InGame.Block.Index
{
    internal sealed class BlockTypeIndex
    {
        private readonly Dictionary<string, List<BlockGameObject>> _blocksByBlockType = new();

        public IReadOnlyList<BlockGameObject> GetBlocks(string blockType)
        {
            return _blocksByBlockType.TryGetValue(blockType, out var blocks) ? blocks : Array.Empty<BlockGameObject>();
        }

        public void Register(BlockGameObject block)
        {
            // 種別ごとに追加し、駅一覧の全件走査を避ける
            // Index each block by type to avoid scanning all blocks for stations
            var blockType = block.BlockMasterElement.BlockType;
            if (!_blocksByBlockType.TryGetValue(blockType, out var blocks))
            {
                blocks = new List<BlockGameObject>();
                _blocksByBlockType.Add(blockType, blocks);
            }
            blocks.Add(block);
        }

        public void Remove(BlockGameObject block)
        {
            if (!_blocksByBlockType.TryGetValue(block.BlockMasterElement.BlockType, out var blocks)) return;
            blocks.Remove(block);
        }
    }
}
