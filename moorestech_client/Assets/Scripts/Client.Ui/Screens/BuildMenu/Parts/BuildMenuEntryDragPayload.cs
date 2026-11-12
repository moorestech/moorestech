using System;
using Client.Ui.Foundation.DragDrop;
using Client.Ui.State.Hotbar;
using Client.Ui.State.Hotbar.Logic;

namespace Client.Ui.Screens.BuildMenu.Parts
{
    /// <summary>
    /// エントリのドラッグ源。タップは選択、落としはホットバーの受け口（IHotbarDropReceiver）へ、枠外は IHotbarCommands.Drop へ渡す
    /// Drag source of an entry; a tap selects, a drop goes to the hotbar receiver (IHotbarDropReceiver), and an outside drop goes to IHotbarCommands.Drop
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
            // ホットバーの印へ源を渡す。別の型の落とし先は Foundation が飛ばし OnDroppedOutside になる
            // Hand the source to the hotbar mark; other target types are skipped by Foundation into OnDroppedOutside
            receiver.Receive(_source);
        }

        public void OnDroppedOutside()
        {
            // 枠外も規則の正本へ渡し、何も起きないことは HotbarDropResolver が決める（D-11）
            // Outside drops also go to the single rule source; HotbarDropResolver decides that nothing happens (D-11)
            _hotbarCommands.Drop(_source, OutsideHotbarDropTarget.Instance);
        }
    }
}
