using Client.Game.InGame.UI.UIState.State;
namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    /// <summary>
    ///     削除ツールの照準の絞り込み条件。最前面か、指定カテゴリーの最前面かの2択（nullを合図に使わない）
    ///     Aim filter of the delete tool: plain frontmost, or frontmost within a category (null is never used as a signal)
    /// </summary>
    public readonly struct DeleteAimFilter
    {
        public static DeleteAimFilter Frontmost => new(false, string.Empty);

        public static DeleteAimFilter Category(string categoryKey)
        {
            return new DeleteAimFilter(true, categoryKey);
        }

        public bool IsCategoryRequired { get; }
        private readonly string _categoryKey;

        private DeleteAimFilter(bool isCategoryRequired, string categoryKey)
        {
            IsCategoryRequired = isCategoryRequired;
            _categoryKey = categoryKey;
        }

        // 最前面条件は何でも受け、カテゴリー条件は一致したものだけ受ける
        // Frontmost accepts anything; a category filter accepts only matching targets
        public bool Accepts(IDeleteTarget target)
        {
            if (!IsCategoryRequired) return true;
            return target.GetDestructionCategory() == _categoryKey;
        }
    }
}
