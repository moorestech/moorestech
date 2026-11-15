namespace Client.Ui.Foundation.Composite
{
    /// <summary>
    /// sRGB 合成の RT を画面の大きさへ合わせた結果
    /// The outcome of fitting the sRGB composite RT to the screen size
    /// </summary>
    internal enum UiSrgbPanelResizeResult
    {
        // 同じ大きさか最小化中は RT を保つ
        // Same size or minimised; the current RT stays
        Unchanged,

        // 新しい大きさの RT を作って差し替えた
        // An RT of the new size was created and swapped in
        Recreated,

        // 作れなかったので前の RT のまま。LogError を出した
        // Creation failed, so the previous RT stays; a LogError was emitted
        CreationFailed,

        // 前回作れなかった大きさと同じなので試さず前の RT のまま（ログも出さない）
        // Same size as the last failure, so it was not retried and the previous RT stays (no log either)
        SkippedFailedSize,
    }
}
