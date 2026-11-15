using Client.Ui.Content;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.BuildMenu.CategoryScroll;
using Client.Ui.Screens.BuildMenu.Parts;
using Client.Ui.State.BuildMenu.Commands;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Session;
using Client.Ui.State.Hotbar;
using Client.Ui.State.Routing;
using Client.Ui.State.Toast;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BuildMenu
{
    /// <summary>
    /// ビルドメニューの画面（Web BuildMenuPanel の写し）。VM と検索語から表示リストを合成して 3 列へ配る
    /// Build-menu screen (port of the Web BuildMenuPanel); composes the shown list from the view model and query and feeds the three columns
    /// </summary>
    public sealed class BuildMenuView : VisualElement
    {
        private readonly BuildMenuSessionStore _session;
        private readonly BuildMenuCategoryScrollTracker _tracker;
        private readonly BuildMenuPanelSkeleton _skeleton;
        private readonly BuildMenuCategorySidebar _sidebar;
        private readonly BuildMenuCategoryList _list;
        private readonly BuildMenuEntryActions _actions;
        private readonly BuildMenuScrollBinding _scrollBinding;
        private BuildMenuViewModel _viewModel = BuildMenuViewModel.Empty;
        private string _query;
        private bool _shown;

        public BuildMenuView(IBuildMenuCommands commands, IHotbarCommands hotbarCommands, GameContentPresentation content, PlacementTargetIcons icons, UiCommandResultToaster toaster, BuildMenuSessionStore session, BuildMenuCategoryScrollTracker tracker, IUiOsCursor osCursor)
        {
            _session = session;
            _tracker = tracker;
            _query = session.Current.Query;
            AddToClassList("build-menu");
            pickingMode = PickingMode.Ignore;

            // 3 列の部品を作り、骨へ並べる
            // Build the three columns' parts and lay them on the skeleton
            _sidebar = new BuildMenuCategorySidebar(osCursor);
            var detail = new BuildMenuDetailSidebar(content, icons);
            _actions = new BuildMenuEntryActions(commands, toaster, session, detail);
            _list = new BuildMenuCategoryList(content, icons, _actions, hotbarCommands);
            _skeleton = new BuildMenuPanelSkeleton(_sidebar, _list, detail, osCursor);

            Add(_skeleton.Band);
            _scrollBinding = new BuildMenuScrollBinding(_skeleton.Scroll, _list, tracker);

            // 閉じる・検索・押下・現在地の追従
            // Close, search, category presses and following the current category
            _skeleton.CloseButton.OnClicked.Subscribe(_ => toaster.Report(UiStateTransitionRules.RequestCommandName, commands.Close()));
            _skeleton.Search.OnValueChanged.Subscribe(OnQueryChanged);
            _sidebar.OnCategorySelected.Subscribe(tracker.JumpTo);
            tracker.ActiveCategory.Subscribe(_sidebar.SetActive);

            // 生成直後は隠す（SetShownで出す）
            // Start hidden; the presenter's SetShown reveals it
            UiDisplay.SetShown(this, false);

            #region Internal

            void OnQueryChanged(string query)
            {
                _query = query;
                _session.SetQuery(query);
                RenderShownList();
            }

            #endregion
        }

        public void Render(BuildMenuViewModel viewModel)
        {
            // 開いている間の再配信でも sticky の詳細を引き直す（Web と同じ。A-57・D-19）
            // Re-resolve the sticky detail on every rebroadcast while open (same as the Web; A-57, D-19)
            _viewModel = viewModel;
            _actions.SetViewModel(viewModel);
            RenderShownList();
        }

        public void SetShown(bool shown)
        {
            // silent-ok: 同じ状態の渡し直しは冪等 / 表示は変わらない
            // silent-ok: passing the same state again is idempotent / nothing visible changes
            if (shown == _shown) return;
            _shown = shown;
            if (!shown)
            {
                // 位置を保存して視口を凍らせてから隠す（隠した ScrollView の位置は当てにならないため。D-7）
                // Save the position and freeze the viewport before hiding, since a hidden ScrollView's offset is unreliable (D-7)
                _scrollBinding.Detach();
                UiDisplay.SetShown(this, false);
                return;
            }

            // 開くたびにセッションの検索語を戻し、出してから新しい視口を張る（D-7）
            // On every open restore the session query, show, then attach a fresh viewport (D-7)
            _query = _session.Current.Query;
            _skeleton.Search.SetValueWithoutNotify(_query);
            UiDisplay.SetShown(this, true);
            RenderShownList();
            _scrollBinding.Attach();
        }

        private void RenderShownList()
        {
            // サイドバーは絞り込み前、リストは絞り込み後の群を見る（Web と同じ）
            // The sidebar sees unfiltered groups and the list filtered ones (same as the Web)
            var shown = BuildMenuShownListComposer.Compose(_viewModel, _query);
            _sidebar.Render(shown, _viewModel);
            _list.Render(shown.Groups, _viewModel);
            _skeleton.SetNoResults(shown.ShowsNoResults);
            _tracker.SetVisibleCategories(shown.ShownCategoryGuids);
            _sidebar.SetActive(_tracker.ActiveCategory.Value);
        }
    }
}
