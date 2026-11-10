using Client.Ui.State.BlockInventory.Logic.Detail;
using Game.Block.Blocks.Machine;

namespace Client.Ui.State.BlockInventory.Machine.Detail
{
    /// <summary>
    /// サーバーの機械の状態文字列を表示の状態へ写す（Web ホストは camelCase の文字列で渡していた）
    /// Maps the server's machine state string onto the display state (the Web host passed it as a camelCase string)
    /// </summary>
    public static class MachineStateTypeConversion
    {
        public static bool TryConvert(string stateType, out MachineProcessDisplayState state)
        {
            switch (stateType)
            {
                case VanillaMachineBlockStateConst.IdleState:
                    state = MachineProcessDisplayState.Idle;
                    return true;
                case VanillaMachineBlockStateConst.ProcessingState:
                    state = MachineProcessDisplayState.Processing;
                    return true;
                case VanillaMachineBlockStateConst.HaltedState:
                    state = MachineProcessDisplayState.Halted;
                    return true;
                default:
                    state = MachineProcessDisplayState.Idle;
                    return false;
            }
        }
    }
}
