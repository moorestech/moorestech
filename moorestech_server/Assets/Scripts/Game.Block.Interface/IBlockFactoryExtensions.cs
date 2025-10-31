using System;
using Core.Master;

namespace Game.Block.Interface
{
    public static class IBlockFactoryExtensions
    {
        // 既存呼び出しの互換性を保ちつつ生成パラメータを補完
        // Preserve legacy call sites while providing creation parameters
        public static IBlock Create(this IBlockFactory blockFactory, BlockId blockId, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return blockFactory.Create(blockId, blockInstanceId, blockPositionInfo, Array.Empty<BlockCreateParam>());
        }
    }
}
