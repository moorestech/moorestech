using Client.Ui.State.Hotbar;
using Client.Ui.State.Hotbar.Logic;

namespace Client.Ui.Screens.Hotbar.Parts
{
    /// <summary>
    /// 列の枠間の隙間の印。落としを列の隙間として操作へ渡す
    /// Gap mark in the row; hands the drop to the commands as the row gap
    /// </summary>
    public sealed class HotbarRowGapDropMark : IHotbarDropReceiver
    {
        private readonly IHotbarCommands _commands;

        public HotbarRowGapDropMark(IHotbarCommands commands)
        {
            _commands = commands;
        }

        // 隙間は枠外（解除）と別の先として渡し、何が起きるかは HotbarDropResolver が決める
        // Pass the gap as a target distinct from outside (clear); HotbarDropResolver decides the outcome
        public void Receive(HotbarDragSource source)
        {
            _commands.Drop(source, HotbarRowGapDropTarget.Instance);
        }
    }
}
