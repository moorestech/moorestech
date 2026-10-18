namespace Client.Game.InGame.Block.DistanceCulling
{
    /// <summary>
    /// ブロックから距離カリングの範囲と対象を作る（用途ごとに実装）
    /// Builds the culling bounds and target from a block (implemented per use)
    /// </summary>
    public interface IBlockDistanceCullingSource
    {
        BlockDistanceCullingEntry CreateEntry(BlockGameObject block);
    }
}
