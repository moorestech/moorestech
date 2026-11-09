namespace Client.Ui.State.Tutorial.Logic
{
    /// <summary>
    /// アンカーの解決状態の理由。状態の理由を表す唯一の enum（Web の resolveAnchor の理由に対応）
    /// The reason of an anchor's resolution; the single enum for state reasons (mirrors Web resolveAnchor reasons)
    /// </summary>
    public enum TutorialAnchorReason
    {
        Mounted,
        Missing,
        DuplicateAnchor,
        DisplayNone,
        VisibilityHidden,
        ZeroArea,
        OutsideViewport,
    }
}
