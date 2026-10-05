namespace Client.Game.InGame.UI.UIState.State.RemovePreview
{
    /// <summary>
    ///     削除プレビュー（赤表示）の付け外しだけを持つ表示対象
    ///     Display target that only toggles the remove (red) preview
    /// </summary>
    public interface IRemovePreviewable
    {
        // 要求者ごとに赤を求める。誰かが求めている間は赤のまま
        // Request red per requester; it stays red while anyone still requests it
        void RequestRemovePreview(object requester);
        void ReleaseRemovePreview(object requester);
    }
}
