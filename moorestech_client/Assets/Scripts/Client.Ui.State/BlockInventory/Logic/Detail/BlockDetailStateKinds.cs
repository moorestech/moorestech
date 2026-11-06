namespace Client.Ui.State.BlockInventory.Logic.Detail
{
    // 歯車ネットワークの停止理由。Web の GearNetworkStopReason（サーバーの GearNetworkStopReason と同じ 5 値）
    // Gear network stop reason; Web GearNetworkStopReason (the same five values as the server enum)
    public enum GearNetworkStopReasonKind
    {
        None,
        Rocked,
        OverRequirePower,
        NoGeneration,
        NoGenerator,
    }

    // 機械の公開状態。出力詰まりはサーバーが idle として公開するので 3 値（Web の MachineProcessState）
    // A machine's published state; output blockage is published as idle, so three values (Web MachineProcessState)
    public enum MachineProcessDisplayState
    {
        Idle,
        Processing,
        Halted,
    }

    // 歯車→電力変換機の発電状態（Web の GearToElectricGenerationState）
    // Generation state of the gear-to-electric generator (Web GearToElectricGenerationState)
    public enum GearToElectricDisplayState
    {
        Generating,
        Idle,
        Stopped,
    }
}
