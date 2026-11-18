using System;
using Client.Ui.Foundation.DragDrop;
using Client.Ui.State.Hotbar;
using Client.Ui.State.Hotbar.Logic;

namespace Client.Ui.Screens.BuildMenu.Parts
{
    /// <summary>
    /// エントリのドラッグ源。タップは選択、落としは受け口へ
    /// Entry drag source; tap selects, drop goes to the receiver
    /// </summary>
    public sealed class BuildMenuEntryDragPayload : IUiDragPayload<IHotbarDropReceiver>
    {
        private readonly IBuildMenuEntryHandler _handler;
        private readonly IHotbarCommands _hotbarCommands;

        private readonly BuildMenuEntryDragSource _source;

        public BuildMenuEntryDragPayload(Guid targetId, IBuildMenuEntryHandler handler, IHotbarCommands hotbarCommands)
        {
            _source = new BuildMenuEntryDragSource(targetId);
            _handler = handler;
            _hotbarCommands = hotbarCommands;
        }

        public void OnTapped()
        {
            _handler.OnEntrySelected(_source.TargetId);
        }

        public void OnDropped(IHotbarDropReceiver receiver)
        {
            // ホットバーの印へ源を渡す。他の型は枠外
            // Pass source to hotbar mark; other types are outside
            receiver.Receive(_source);
        }

        public void OnDroppedOutside()
        {
            // 枠外も正本へ渡す（D-11）
            // Outside also goes to the rule source (D-11)
            _hotbarCommands.Drop(_source, OutsideHotbarDropTarget.Instance);
        }
    }
}
