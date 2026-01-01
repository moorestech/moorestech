namespace Game.Block.Interface.Component
{
    /// <summary>
    /// アイテム挿入時のコンテキスト情報
    /// Item insertion context information
    /// </summary>
    public readonly struct InsertItemContext
    {
        /// <summary>
        /// 挿入元ブロックのインスタンスID
        /// Source block instance ID
        /// </summary>
        public BlockInstanceId SourceBlockInstanceId { get; }

        /// <summary>
        /// 挿入元コネクターの情報
        /// Source connector information
        /// </summary>
        public BlockConnectorInfo SourceConnector { get; }

        /// <summary>
        /// 挿入先コネクターの情報
        /// Target connector information
        /// </summary>
        public BlockConnectorInfo TargetConnector { get; }

        public InsertItemContext(BlockInstanceId sourceBlockInstanceId, BlockConnectorInfo sourceConnector, BlockConnectorInfo targetConnector)
        {
            SourceBlockInstanceId = sourceBlockInstanceId;
            SourceConnector = sourceConnector;
            TargetConnector = targetConnector;
        }

        public static InsertItemContext Empty => new(default, null, null);
    }
}
