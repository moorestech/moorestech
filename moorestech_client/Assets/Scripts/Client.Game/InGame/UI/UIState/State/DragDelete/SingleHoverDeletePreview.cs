namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    // 単体ホバーを所有し、戻しより先に参照を手放す
    // Own the single hover and detach it before resetting
    public class SingleHoverDeletePreview
    {
        private readonly DeletePreviewResetRecovery _resetRecovery;
        public IDeleteTarget Current { get; private set; }

        public SingleHoverDeletePreview(DeletePreviewResetRecovery resetRecovery)
        {
            _resetRecovery = resetRecovery;
        }

        public void Hover(IDeleteTarget hovered)
        {
            if (Current == hovered) return;

            // 旧対象を解放し、未完了対象の再保持を防ぐ
            // Release the previous target and prevent reacquiring an incomplete reset
            Release();
            if (!_resetRecovery.CanPreview(hovered)) return; // silent-ok: 失敗対象を再保持しない / 回復モデルが初回警告を記録する
            Current = hovered;
            hovered.SetRemovePreviewing();
        }

        public void Release()
        {
            if (Current == null) return;

            // 例外が発生しても同じ参照を再度解放しない
            // Never release the same retained reference again after an exception
            var previous = Current;
            Current = null;
            _resetRecovery.Reset(previous, "[DragDelete] hovered target was destroyed before reset");
        }
    }
}
