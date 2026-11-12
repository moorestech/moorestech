using Client.Ui.State.Hotbar;
using Client.Ui.State.Hotbar.Logic;

namespace Client.Ui.Screens.Hotbar.Parts
{
    /// <summary>
    /// 枠の落とし先の印。落としを枠番号付きの先として操作へ渡す
    /// Slot drop-target mark; hands the drop to the commands as a slot target
    /// </summary>
    public sealed class HotbarSlotDropMark : IHotbarDropReceiver
    {
        private readonly IHotbarCommands _commands;

        private readonly int _slotIndex;

        public HotbarSlotDropMark(int slotIndex, IHotbarCommands commands)
        {
            _slotIndex = slotIndex;
            _commands = commands;
        }

        // 何が起きるか（割当・入替・無操作）は HotbarDropResolver が決める
        // HotbarDropResolver decides the outcome (assign, swap or nothing)
        public void Receive(HotbarDragSource source)
        {
            _commands.Drop(source, new HotbarSlotDropTarget(_slotIndex));
        }
    }
}
