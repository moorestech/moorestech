namespace Game.Block.Interface
{
    /// <summary>
    /// ブロック削除の理由を表す列挙体
    /// Enum representing block removal reasons
    /// </summary>
    public enum BlockRemovalType
    {
        /// <summary>
        /// プレイヤーによる手動削除
        /// Manual removal by player
        /// </summary>
        ManualRemove,
        
        /// <summary>
        /// システムによる破壊（過負荷など）
        /// Broken by system (e.g., overload)
        /// </summary>
        Broken
    }
}
