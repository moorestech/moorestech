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

        // 隙間は枠外と別の先で渡し結果は Resolver が決める
        // Pass the gap apart from outside; Resolver decides
        public void Receive(HotbarDragSource source)
        {
            _commands.Drop(source, HotbarRowGapDropTarget.Instance);
        }
    }
}
