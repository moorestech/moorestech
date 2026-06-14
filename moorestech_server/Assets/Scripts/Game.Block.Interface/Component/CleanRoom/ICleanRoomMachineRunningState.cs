namespace Game.Block.Interface.Component
{
    // 専用機械の稼働計量IF。CleanRoomPollutionCalculator が IsRunning=true の台数を A_machine の係数に使う。
    // Running-state metering IF for the dedicated machine; the calculator counts IsRunning==true units for A_machine.
    public interface ICleanRoomMachineRunningState : IBlockComponent
    {
        // 稼働（汚染）中なら true（CurrentState==Processing）。
        // True while running (polluting): CurrentState==Processing.
        bool IsRunning { get; }
    }
}
