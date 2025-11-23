namespace Game.Block.Interface
{
    /// <summary>
    /// ブロック破壊機能を提供するインターフェース
    /// Interface for block removal functionality
    /// </summary>
    public interface IBlockRemover
    {
        /// <summary>
        /// 指定位置のブロックを削除する
        /// Remove the block at the specified position
        /// </summary>
        /// <param name="position">削除するブロックの位置情報 / Position info of the block to remove</param>
        /// <param name="removalType">削除理由 / Removal reason</param>
        void RemoveBlock(BlockPositionInfo position, BlockRemovalType removalType);
    }
}

