namespace Game.Block.Interface
{
    // ブロック削除の理由を表す列挙型
    // Enum representing the reason for block removal
    public enum BlockRemoveReason
    {
        // システムによる破壊（過負荷など）
        // Broken by system (overload, etc.)
        Broken,
        
        // 手動削除（プレイヤーによる削除）
        // Manual removal (by player)
        ManualRemove
    }
}

