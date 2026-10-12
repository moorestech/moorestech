using Client.Game.InGame.UI.UIState.State;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    /// <summary>
    ///     照準の結果。外れた場合は理由を持つ（照準の外れは毎フレームの通常状態なのでログは出さない）
    ///     Aim result; carries the reason on a miss (a miss is the normal per-frame state, so it is not logged)
    /// </summary>
    public enum DeleteAimOutcome
    {
        Found,
        NothingHit,
        OccludedByNonTarget,
        // 対象はあるが固定カテゴリーに合わない
        // Targets exist, but none matches the fixed category
        NoTargetOfCategory,
    }

    public readonly struct DeleteAimResult
    {
        public DeleteAimOutcome Outcome { get; }
        public IDeleteTarget Target { get; }

        private DeleteAimResult(DeleteAimOutcome outcome, IDeleteTarget target)
        {
            Outcome = outcome;
            Target = target;
        }

        // 別カテゴリーだけの照準はドラッグ拒否理由へ変換する
        // Convert aim at only other categories to a drag denial reason
        public LocalizationKey? GetDragDenyReason()
        {
            return Outcome == DeleteAimOutcome.NoTargetOfCategory ? LocalizationKeys.Ui.Delete.DifferentCategorySelection : null;
        }

        public static DeleteAimResult Found(IDeleteTarget target)
        {
            return new DeleteAimResult(DeleteAimOutcome.Found, target);
        }

        public static DeleteAimResult Missed(DeleteAimOutcome outcome)
        {
            return new DeleteAimResult(outcome, null);
        }
    }
}
