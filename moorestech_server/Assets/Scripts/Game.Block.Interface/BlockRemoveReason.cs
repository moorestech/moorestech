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
        ManualRemove,
        
        // 張替え設置による撤去。直後に同セルへ新ブロックが設置される
        // Removal by replace placement; a new block is placed on the same cell right after
        Replace
    }
}

