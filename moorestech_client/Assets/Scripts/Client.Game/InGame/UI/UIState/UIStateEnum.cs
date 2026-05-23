namespace Client.Game.InGame.UI.UIState
{
    public enum UIStateEnum
    {
        GameScreen,
        PlayerInventory,
        SubInventory,  // 統一インベントリステート（ブロック・列車共通）
        PauseMenu,
        DeleteBar,
        Story,
        PlaceBlock,
        ChallengeList,
        ResearchTree,
        Debug,
        // 列車に乗車中の HUD ステート。現状は GameScreen と同じ見た目だが、
        // 将来的に乗車専用 UI を表示する余地として独立させる。
        // HUD state while riding a train. Currently looks identical to GameScreen but
        // kept separate so riding-only UI can be added later.
        TrainHUDScreen,
    }
}
