using System;
using Client.Ui.Foundation.DragDrop;
using Client.Ui.State.Hotbar.Logic;

namespace Client.Ui.Screens.BuildMenu.Parts
{
    /// <summary>
    /// エントリのドラッグ源。タップは選択、落としはホットバーの受け口（IHotbarDropReceiver）へ渡す
    /// Drag source of an entry; a tap selects, and a drop goes to the hotbar receiver (IHotbarDropReceiver)
    /// </summary>
    public sealed class BuildMenuEntryDragPayload : IUiDragPayload<IHotbarDropReceiver>
    {
        private readonly IBuildMenuEntryHandler _handler;

        public BuildMenuEntryDragSource Source { get; }

        public BuildMenuEntryDragPayload(Guid targetId, IBuildMenuEntryHandler handler)
        {
            Source = new BuildMenuEntryDragSource(targetId);
            _handler = handler;
        }

        public void OnTapped()
        {
            _handler.OnEntrySelected(Source.TargetId);
        }

        public void OnDropped(IHotbarDropReceiver receiver)
        {
            // ホットバーの印へ源を渡す。別の型の落とし先は Foundation が飛ばし OnDroppedOutside になる。断った理由のログは受け手が出す（D-16）
            // Hand the source to the hotbar mark; other target types are skipped by Foundation into OnDroppedOutside; the receiver logs any refusal (D-16)
            receiver.Receive(Source);
        }

        public void OnDroppedOutside()
        {
            // silent-ok: 正常操作の無操作でありガードの縮退ではない（Web の HotbarDropResolver も何もしない） / 遊ぶ人は何も起きないことで分かる
            // silent-ok: a normal no-op, not a degraded guard (the Web HotbarDropResolver does nothing either) / the player sees nothing happened
        }
    }
}
