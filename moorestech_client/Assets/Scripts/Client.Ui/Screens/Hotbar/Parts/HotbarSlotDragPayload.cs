using Client.Ui.Foundation.DragDrop;
using Client.Ui.State.Hotbar;
using Client.Ui.State.Hotbar.Logic;
using Client.Ui.State.Toast;

namespace Client.Ui.Screens.Hotbar.Parts
{
    /// <summary>
    /// 枠の源。タップは選択、ドロップは割当の入替・解除（Web HotbarCell の useHotbarDragSource）
    /// A slot as a source: a tap selects, a drop swaps or clears the assignment (Web HotbarCell useHotbarDragSource)
    /// </summary>
    public sealed class HotbarSlotDragPayload : IUiDragPayload<IHotbarDropReceiver>
    {
        private readonly int _slotIndex;
        private readonly HotbarSlotViewModel _slot;
        private readonly IHotbarCommands _commands;
        private readonly UiCommandResultToaster _toaster;

        public HotbarSlotDragPayload(int slotIndex, HotbarSlotViewModel slot, IHotbarCommands commands, UiCommandResultToaster toaster)
        {
            _slotIndex = slotIndex;
            _slot = slot;
            _commands = commands;
            _toaster = toaster;
        }

        // 受けるかは操作側（HotbarSelectRule）が決め、拒否はトースト口へ（良性なのでログだけ）。D&D は受けない画面でも成立する
        // The operation side (HotbarSelectRule) decides; a rejection goes to the toaster (benign, so log only). Drag-and-drop works even on non-accepting screens
        public void OnTapped()
        {
            _toaster.Report(HotbarCommands.SelectCommandName, _commands.Select(_slotIndex));
        }

        public void OnDropped(IHotbarDropReceiver receiver)
        {
            // silent-ok: 空き枠は掴む物が無い（Web の source === null） / HotbarSlotDragGestureTest「空き枠を引きずって…」が固定する
            if (!TryGetDragSource(out var source)) return;
            // ホットバーの印（枠・列の隙間）だけがここへ来て、受け手が自分の先を添えて Drop へ渡す。他の画面の印は Foundation が飛ばし OnDroppedOutside になる
            // Only hotbar marks (slot, row gap) arrive here and the receiver passes its own target to Drop; other screens' marks are skipped by Foundation into OnDroppedOutside
            receiver.Receive(source);
        }

        public void OnDroppedOutside()
        {
            // silent-ok: 空き枠は掴む物が無い（Web の source === null） / HotbarSlotDragGestureTest「空き枠を引きずって…」が固定する
            if (!TryGetDragSource(out var source)) return;
            // 枠外として渡す（解除になるかは HotbarDropResolver が決める）
            // Pass as outside; HotbarDropResolver decides that it clears
            _commands.Drop(source, OutsideHotbarDropTarget.Instance);
        }

        // 掴める物があるかの判定はここ 1 か所。空き枠は掴む物が無い
        // The only check for something to carry; an empty slot carries nothing
        private bool TryGetDragSource(out HotbarSlotDragSource source)
        {
            source = new HotbarSlotDragSource(_slotIndex);
            // silent-ok: 空き枠はドラッグに確定した後も何もせずタップにも落とさない（Web useHotbarDragSource の source === null） / HotbarSlotDragGestureTest「空枠を閾値超えで…」が固定する
            return _slot.IsAssigned;
        }
    }
}
