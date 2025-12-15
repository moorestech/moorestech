namespace Client.Game.InGame.UI.UIState.State
{
    /// <summary>
    ///     削除可能なオブジェクトを表すインターフェース
    ///     Interface representing an object that can be deleted
    /// </summary>
    public interface IDeleteTarget
    {
        /// <summary>
        ///     RemovePreviewの表示
        ///     Display the remove preview
        /// </summary>
        void SetRemovePreviewing();

        /// <summary>
        ///     Materialをもとに戻す
        ///     Reset material to original state
        /// </summary>
        void ResetMaterial();
    }
}