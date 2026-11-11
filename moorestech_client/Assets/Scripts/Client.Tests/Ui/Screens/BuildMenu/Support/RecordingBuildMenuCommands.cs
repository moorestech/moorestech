using System;
using System.Collections.Generic;
using Client.Ui.State.BuildMenu.Commands;
using Client.Ui.State.Commands;
using Cysharp.Threading.Tasks;

namespace Client.Tests.Ui.Screens.BuildMenu.Support
{
    // 画面から呼ばれたコマンドを記録する。削除の結果だけテストから差し替えられる。public なのは土台 BuildMenuScreenTestHost の protected プロパティの型だから（CS0053）
    // Records commands called by the screen; only the delete result can be swapped by tests
    public sealed class RecordingBuildMenuCommands : IBuildMenuCommands
    {
        private UiCommandResult _deleteResult = UiCommandAccepted.Instance;

        public List<Guid> Selected { get; } = new();
        public List<Guid> DeletedBlueprints { get; } = new();
        public int CloseCount { get; private set; }

        public void SetDeleteResult(UiCommandResult result)
        {
            _deleteResult = result;
        }

        public UiCommandResult SelectEntry(Guid targetId)
        {
            Selected.Add(targetId);
            return UiCommandAccepted.Instance;
        }

        public UniTask<UiCommandResult> DeleteBlueprint(Guid blueprintGuid)
        {
            DeletedBlueprints.Add(blueprintGuid);
            return UniTask.FromResult(_deleteResult);
        }

        public UiCommandResult Close()
        {
            CloseCount++;
            return UiCommandAccepted.Instance;
        }
    }
}
