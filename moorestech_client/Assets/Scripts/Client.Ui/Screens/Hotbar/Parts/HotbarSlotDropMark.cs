using Client.Ui.State.Hotbar;
using Client.Ui.State.Hotbar.Logic;

namespace Client.Ui.Screens.Hotbar.Parts
{
    /// <summary>
    /// 枠の落とし先の印（Web の data-hotbar-slot-index）。受けた源を L0-s3 の規則で解いて操作へ流す
    /// Drop-target mark for a slot (the Web data-hotbar-slot-index); resolves the received source by the L0-s3 rule and sends the operation
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

        // 枠の落としは L0-s3 の規則が全組を解く（断る組は HotbarNoDropAction で、HotbarCommands が silent-ok で受ける）
        // Slot drops resolve every pair by the L0-s3 rule (refused pairs become HotbarNoDropAction, taken as silent-ok by HotbarCommands)
        public void Receive(HotbarDragSource source)
        {
            _commands.Apply(HotbarDropResolver.Resolve(source, new HotbarSlotDropTarget(_slotIndex)));
        }
    }
}
