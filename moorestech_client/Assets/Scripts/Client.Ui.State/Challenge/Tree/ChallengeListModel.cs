using Client.Ui.State.Challenge.Tree.Selection;
using Client.Ui.State.Routing;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Challenge.Tree
{
    /// <summary>
    /// チャレンジ一覧の表示の組み直しと配信（表示中だけ組む）
    /// Rebuilds and publishes the challenge list display (only while shown)
    /// </summary>
    /// <remarks>
    /// 表示中かは控えた旗でなく経路の今の値で読む。一覧を離れると選択ストアも経路を購読して None に戻すが、どちらの購読が先に呼ばれても隠れた後の選択の変化では組まない（DI の登録順に依らない）
    /// Whether shown is read from the router's current value, not a cached flag; on leaving, the selection store also resets to None from its own router subscription, and whichever runs first no rebuild follows a selection change after hiding (independent of DI registration order)
    /// </remarks>
    public sealed class ChallengeListModel : IInitializable
    {
        private readonly ChallengeProgressState _progress;
        private readonly ChallengeCategorySelectionStore _selection;
        private readonly UiRoutingState _routing;
        private readonly ReactiveProperty<ChallengeListViewModel> _current = new(ChallengeListViewModel.Empty);

        public IReadOnlyReactiveProperty<ChallengeListViewModel> Current => _current;

        public ChallengeListModel(ChallengeProgressState progress, ChallengeCategorySelectionStore selection, UiRoutingState routing)
        {
            _progress = progress;
            _selection = selection;
            _routing = routing;
        }

        public void Initialize()
        {
            // 完了の知らせ（Categories の置き換えの後に来る）と選択の変化で組み直す。ゲーム寿命なので購読は解かない
            // Rebuild on completion notices (after Categories is replaced) and selection changes; game lifetime, so subscriptions stay
            _progress.OnChallengeCompleted.Subscribe(_ => RebuildWhileShown());
            _selection.Current.Skip(1).Subscribe(_ => RebuildWhileShown());

            // 一覧の表示の間だけ組む。経路の通知（突入）で 1 回組む（前例 ResearchTreeModel）
            // Build only while the list is shown, once per router notice (entering; precedent ResearchTreeModel)
            _routing.Current.Subscribe(_ => RebuildWhileShown());
        }

        private void RebuildWhileShown()
        {
            // ReactiveProperty は値を書いてから購読者へ流すので、通知の中でも今の経路が読める
            // ReactiveProperty stores the value before notifying, so the current route is readable inside any notice
            if (!ChallengeScreenVisibility.IsShown(_routing.Current.Value)) return; // silent-ok: 閉画面では組まない / 閉画面で組まず突入で組むことは L0 ChallengeListModelTest が固定し、録画 uitk-challenge-list の開き直しの段が見る
            _current.Value = ChallengeListViewModelFactory.Create(_progress.Categories, _selection.Current.Value);
        }
    }
}
