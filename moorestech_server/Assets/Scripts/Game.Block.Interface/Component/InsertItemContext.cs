using Mooresmaster.Model.BlockConnectInfoModule;

namespace Game.Block.Interface.Component
{
    /// <summary>
    /// アイテム挿入時のコンテキスト情報
    /// Item insertion context information
    /// </summary>
    public readonly struct InsertItemContext
    {
        /// <summary>
        /// 挿入元コネクターの情報
        /// Source connector information
        /// </summary>
        public BlockConnectInfoElement SourceConnector { get; }

        /// <summary>
        /// 挿入先コネクターの情報
        /// Target connector information
        /// </summary>
        public BlockConnectInfoElement TargetConnector { get; }

        public InsertItemContext(BlockConnectInfoElement sourceConnector, BlockConnectInfoElement targetConnector)
        {
            SourceConnector = sourceConnector;
            TargetConnector = targetConnector;
        }

        public static InsertItemContext Empty => new InsertItemContext(null, null);
    }
}
