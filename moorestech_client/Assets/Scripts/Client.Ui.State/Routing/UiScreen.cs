namespace Client.Ui.State.Routing
{
    /// <summary>
    /// UIState から解決した描画上の画面。Web の UiScreen と同じ語彙
    /// The rendered screen resolved from UIState; same vocabulary as the web UiScreen
    /// </summary>
    public enum UiScreen
    {
        None,
        PlayerInventory,
        SubInventory,
        ResearchTree,
        BuildMenu,
        ChallengeList,
        PauseMenu,
        TrainHud,
        TrainPause,
        SkitPause,
    }
}
