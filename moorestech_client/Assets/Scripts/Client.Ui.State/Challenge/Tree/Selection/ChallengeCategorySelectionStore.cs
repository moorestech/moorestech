using System;
using Client.Ui.State.Routing;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Challenge.Tree.Selection
{
    /// <summary>
    /// 選んだカテゴリ（離れたら先頭へ戻す）
    /// Selected category (reset to the first on leaving)
    /// </summary>
    public sealed class ChallengeCategorySelectionStore : IInitializable
    {
        private readonly UiRoutingState _routing;
        private readonly ReactiveProperty<ChallengeCategorySelection> _current = new(ChallengeCategorySelection.None);

        public IReadOnlyReactiveProperty<ChallengeCategorySelection> Current => _current;

        public ChallengeCategorySelectionStore(UiRoutingState routing)
        {
            _routing = routing;
        }

        public void Initialize()
        {
            _routing.Current.Subscribe(snapshot =>
            {
                // silent-ok: 一覧の中の経路の通知は選択を保つ / 離れて無しに戻ることは L0 ChallengeCategorySelectionStoreTest、先頭へ戻ることは L1 ChallengeListViewportTest と録画 uitk-challenge-list が見る
                if (ChallengeScreenVisibility.IsShown(snapshot)) return;
                _current.Value = ChallengeCategorySelection.None;
            });
        }

        public void Select(Guid categoryGuid)
        {
            _current.Value = ChallengeCategorySelection.Of(categoryGuid);
        }
    }
}
