using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    public enum DeleteAddKind
    {
        Added,
        AlreadySelected,
        StrokeDead,
        NotRemovable,
        CategoryMismatch,
    }

    /// <summary>
    ///     なぞり追加の結果。失敗理由を種類で区別し、表示すべき拒否理由があるときだけキーを運ぶ
    ///     Outcome of a stroke add: failure kinds are distinct, and a deny-reason key rides along only when one is displayable
    /// </summary>
    public readonly struct DeleteAddOutcome
    {
        public static DeleteAddOutcome Added => new(DeleteAddKind.Added, null);
        public static DeleteAddOutcome AlreadySelected => new(DeleteAddKind.AlreadySelected, null);
        public static DeleteAddOutcome StrokeDead => new(DeleteAddKind.StrokeDead, null);
        public static DeleteAddOutcome NotRemovable(LocalizationKey? reason) => new(DeleteAddKind.NotRemovable, reason);
        public static DeleteAddOutcome CategoryMismatch() => new(DeleteAddKind.CategoryMismatch, LocalizationKeys.Ui.Delete.DifferentCategorySelection);

        public DeleteAddKind Kind { get; }
        public LocalizationKey? DenyReason { get; }
        public bool IsAdded => Kind == DeleteAddKind.Added;

        private DeleteAddOutcome(DeleteAddKind kind, LocalizationKey? denyReason)
        {
            Kind = kind;
            DenyReason = denyReason;
        }
    }
}
