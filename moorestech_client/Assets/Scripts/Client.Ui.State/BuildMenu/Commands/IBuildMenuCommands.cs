using System;

namespace Client.Ui.State.BuildMenu.Commands
{
    /// <summary>
    /// ビルドメニューの型付きコマンド（Web の build_menu.select・blueprint.delete・ui_state.request の写し）。結果は実装がトーストへ渡す
    /// Typed build-menu commands (port of Web build_menu.select, blueprint.delete and ui_state.request); the implementation hands results to the toaster
    /// </summary>
    public interface IBuildMenuCommands
    {
        void SelectEntry(Guid targetId);
        void DeleteBlueprint(Guid blueprintGuid);
        void Close();
    }
}
