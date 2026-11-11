using System;
using Client.Ui.Screens.BuildMenu.Parts;
using Client.Ui.State.BuildMenu.Commands;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Session;
using Client.Ui.State.Toast;
using Cysharp.Threading.Tasks;

namespace Client.Ui.Screens.BuildMenu
{
    /// <summary>
    /// マスの操作を型付きコマンドへ流し、結果をトーストへ、ホバーを詳細とセッションへ渡す
    /// Routes slot operations to typed commands, results to toasts, and hovers to the detail and session
    /// </summary>
    public sealed class BuildMenuEntryActions : IBuildMenuEntryHandler
    {
        // トーストのログに出す名前は Web の action 名に揃える
        // Names logged with toasts match the Web action names
        private const string SelectCommandName = "build_menu.select";
        private const string DeleteCommandName = "blueprint.delete";

        private readonly IBuildMenuCommands _commands;
        private readonly UiCommandResultToaster _toaster;
        private readonly BuildMenuSessionStore _session;
        private readonly BuildMenuDetailSidebar _detail;
        private BuildMenuViewModel _viewModel = BuildMenuViewModel.Empty;

        public BuildMenuEntryActions(IBuildMenuCommands commands, UiCommandResultToaster toaster, BuildMenuSessionStore session, BuildMenuDetailSidebar detail)
        {
            _commands = commands;
            _toaster = toaster;
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
            _toaster.Report(SelectCommandName, _commands.SelectEntry(targetId));
        }

        public void OnBlueprintDeleteRequested(Guid targetId)
        {
            DeleteBlueprintAsync(targetId).Forget();
        }

        public void OnEntryHovered(Guid targetId)
        {
            _session.SetHoveredEntry(targetId);
            RefreshDetail();
        }

        private async UniTaskVoid DeleteBlueprintAsync(Guid blueprintGuid)
        {
            var result = await _commands.DeleteBlueprint(blueprintGuid);
            _toaster.Report(DeleteCommandName, result);
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
