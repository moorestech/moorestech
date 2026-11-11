using System;
using Client.Ui.State.Commands;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.BuildMenu.Commands
{
    /// <summary>
    /// ビルドメニューの型付きコマンド（Web の build_menu.select・blueprint.delete・ui_state.request の写し）
    /// Typed build-menu commands (port of Web build_menu.select, blueprint.delete and ui_state.request)
    /// </summary>
    public interface IBuildMenuCommands
    {
        UiCommandResult SelectEntry(Guid targetId);
        UniTask<UiCommandResult> DeleteBlueprint(Guid blueprintGuid);
        UiCommandResult Close();
    }
}
