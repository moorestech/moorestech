namespace Game.Construction
{
    /// <summary>
    /// 1セルの張替え結果。呼び出し側はこれを集計してプレイヤーへ通知する
    /// The outcome of one replace cell; the caller aggregates these and notifies the player
    /// </summary>
    public enum ReplacePlacementResult
    {
        Replaced,
        NoChange,
        NotUnlocked,
        CostShortage,
        InventoryFull,
        Rejected,
    }
}
