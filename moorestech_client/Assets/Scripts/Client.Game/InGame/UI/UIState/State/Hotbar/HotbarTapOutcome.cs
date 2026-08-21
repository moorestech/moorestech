namespace Client.Game.InGame.UI.UIState.State.Hotbar
{
    // ホットバータップの分類。呼び出し側は網羅switchで扱う
    // The classification of a hotbar tap; call sites handle it with an exhaustive switch
    public enum HotbarTapOutcome
    {
        // タップが無い、または建築へ使えない枠だった
        // No tap arrived, or the tapped slot cannot drive a build action
        None,

        // 建築モードへ入る（遷移あり）
        // Enters build mode (carries a transit)
        EnterBuildMode,

        // 建築モードを抜ける（遷移あり）
        // Leaves build mode (carries a transit)
        ExitBuildMode,

        // 遷移せず設置対象だけ持ち替えた
        // Swapped the placement target in place, without a screen transition
        SwappedTarget,
    }
}
