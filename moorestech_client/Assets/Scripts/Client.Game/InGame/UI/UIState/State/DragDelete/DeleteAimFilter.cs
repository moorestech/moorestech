using Client.Game.InGame.UI.UIState.State;
namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    /// <summary>
    ///     削除ツール照準の絞り込み条件（最前面か指定カテゴリー）
    ///     Delete-tool aim filter: frontmost or frontmost in a category
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

        // 最前面は全て受け、カテゴリー条件は一致のみ受ける
        // Frontmost accepts all; a category filter accepts only matches
        public bool Accepts(IDeleteTarget target)
        {
            if (!IsCategoryRequired) return true;
            return target.GetDestructionCategory() == _categoryKey;
        }
    }
}
