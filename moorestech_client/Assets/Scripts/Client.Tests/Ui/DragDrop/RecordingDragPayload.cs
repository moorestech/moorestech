using System.Collections.Generic;
using Client.Ui.Foundation.DragDrop;

namespace Client.Tests.Ui.DragDrop
{
    /// <summary>
    /// 源に載せた値の呼ばれ方を記録する（テスト用）。落とし先は名前で区別する
    /// Records how the source payload is called (test double); drop targets are told apart by name
    /// </summary>
    internal sealed class RecordingDragPayload : IUiDragPayload<NamedDropTarget>
    {
        public readonly List<string> Calls = new();

        public void OnTapped()
        {
            Calls.Add("tap");
        }

        public void OnDropped(NamedDropTarget target)
        {
            Calls.Add($"drop:{target.Name}");
        }

        public void OnDroppedOutside()
        {
            Calls.Add("outside");
        }
    }

    internal sealed class NamedDropTarget
    {
        public readonly string Name;

        public NamedDropTarget(string name)
        {
            Name = name;
        }
    }

    // 別の型の落とし先（源の型と違うので飛ばされる）
    // A drop target of another type (skipped because it differs from the source's type)
    internal sealed class OtherKindDropTarget
    {
    }
}
