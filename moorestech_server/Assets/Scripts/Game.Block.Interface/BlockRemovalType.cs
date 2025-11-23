namespace Game.Block.Interface
{
    /// <summary>
    /// ブロック削除の理由を表すEnum
    /// Block removal reason enumeration
    /// </summary>
    public enum BlockRemovalType
    {
        /// <summary>
        /// プレイヤーによる手動削除
        /// Manual removal by player
        /// </summary>
        ManualRemove,

        /// <summary>
        /// システムによる破壊（過負荷等）
        /// Broken by system (e.g., overload)
        /// </summary>
        Broken
    }
}

