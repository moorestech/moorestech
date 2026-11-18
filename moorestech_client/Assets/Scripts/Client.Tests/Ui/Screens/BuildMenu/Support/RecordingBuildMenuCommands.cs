using System;
using System.Collections.Generic;
using Client.Ui.State.BuildMenu.Commands;

namespace Client.Tests.Ui.Screens.BuildMenu.Support
{
    // 画面から呼ばれたコマンドを記録する。public なのは土台 BuildMenuScreenTestHost の protected プロパティの型だから（CS0053）
    // Records commands called by the screen; public because it types a protected property of BuildMenuScreenTestHost (CS0053)
    public sealed class RecordingBuildMenuCommands : IBuildMenuCommands
    {
        public List<Guid> Selected { get; } = new();
        public List<Guid> DeletedBlueprints { get; } = new();
        public int CloseCount { get; private set; }

        public void SelectEntry(Guid targetId)
        {
            Selected.Add(targetId);
        }

        public void DeleteBlueprint(Guid blueprintGuid)
        {
            DeletedBlueprints.Add(blueprintGuid);
        }

        public void Close()
        {
            CloseCount++;
        }
    }
}
