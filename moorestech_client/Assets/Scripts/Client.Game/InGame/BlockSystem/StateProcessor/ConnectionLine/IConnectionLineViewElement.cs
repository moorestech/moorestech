using Game.Block.Interface;

namespace Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine
{
    /// <summary>
    /// ブロック間接続ラインの1本分の表示要素
    /// A single connection line element between two blocks
    /// </summary>
    public interface IConnectionLineViewElement
    {
        // ラインの両端ブロックを設定する
        // Set both endpoint blocks of the line
        void SetLine(BlockInstanceId fromId, BlockInstanceId toId);
    }
}
