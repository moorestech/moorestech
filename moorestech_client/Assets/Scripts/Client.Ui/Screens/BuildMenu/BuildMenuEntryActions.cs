using System;
using Client.Ui.Screens.BuildMenu.Parts;
using Client.Ui.State.BuildMenu.Commands;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Session;

namespace Client.Ui.Screens.BuildMenu
{
    /// <summary>
    /// マス操作を型付きコマンドへ流し、ホバーを詳細とセッションへ
    /// Routes slot operations to typed commands and hovers to the detail and session
    /// </summary>
    public sealed class BuildMenuEntryActions : IBuildMenuEntryHandler
    {
        private readonly IBuildMenuCommands _commands;
        private readonly BuildMenuSessionStore _session;
        private readonly BuildMenuDetailSidebar _detail;
        private BuildMenuViewModel _viewModel = BuildMenuViewModel.Empty;

        public BuildMenuEntryActions(IBuildMenuCommands commands, BuildMenuSessionStore session, BuildMenuDetailSidebar detail)
        {
            _commands = commands;
            _session = session;
            _detail = detail;
        }

        public void SetViewModel(BuildMenuViewModel viewModel)
        {
            _viewModel = viewModel;
            RefreshDetail();
        }

        public void OnEntrySelected(Guid targetId)
        {
            _commands.SelectEntry(targetId);
        }

        public void OnBlueprintDeleteRequested(Guid targetId)
        {
            _commands.DeleteBlueprint(targetId);
        }

        public void OnEntryHovered(Guid targetId)
        {
            _session.SetHoveredEntry(targetId);
            RefreshDetail();
        }

        // sticky: 離れても消さず、エントリが無くなったら（解放の取り消し・BP の削除）ヒントへ戻す（Web の ?? null。D-14）
        // Sticky: never cleared on leave; falls back to the hint once the entry is gone (revoked or deleted), like the Web ?? null (D-14)
        private void RefreshDetail()
        {
            if (_session.Current.HoveredEntryId is { } hovered && _viewModel.TryGetEntry(hovered, out var entry)) _detail.ShowEntry(entry);
            else _detail.ShowHint();
        }
    }
}
