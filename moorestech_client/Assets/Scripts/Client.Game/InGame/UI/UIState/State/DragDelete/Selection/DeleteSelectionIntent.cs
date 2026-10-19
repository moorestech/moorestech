namespace Client.Game.InGame.UI.UIState.State.DragDelete.Selection
{
    /// <summary>
    ///     なぞり・箱が選択へ足すか選択から外すか。押下の瞬間の Ctrl で決まり、その操作の間は変わらない
    ///     Whether a stroke or box adds to or removes from the selection; fixed by Ctrl at the press and unchanged for that operation
    /// </summary>
    public enum DeleteSelectionIntent
    {
        Add,
        Remove,
    }
}
