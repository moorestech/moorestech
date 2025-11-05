namespace Client.Game.InGame.UI.UIState
{
    public enum UIStateEnum
    {
        Current,
        
        GameScreen,
        PlayerInventory,
        SubInventory,  // 統一インベントリステート（ブロック・列車共通）
        PauseMenu,
        DeleteBar,
        Story,
        PlaceBlock,
        ChallengeList,
    }
}