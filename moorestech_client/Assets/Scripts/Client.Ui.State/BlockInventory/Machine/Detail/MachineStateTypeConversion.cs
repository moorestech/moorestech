using Client.Ui.State.BlockInventory.Logic.Detail;
using Game.Block.Blocks.Machine;

namespace Client.Ui.State.BlockInventory.Machine.Detail
{
    /// <summary>
    /// サーバーの状態文字列を表示の状態へ写す
    /// Maps the server's machine state string onto the display state
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
