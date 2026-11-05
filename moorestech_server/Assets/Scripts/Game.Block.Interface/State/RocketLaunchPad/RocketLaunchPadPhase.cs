namespace Game.Block.Interface.State
{
    public enum RocketLaunchPadPhase
    {
        // 資材・積荷・出力口の空きを待つ
        // Waits for materials, payload and output space
        Idle = 0,
        // 初打ち上げ前だけ建造の合図を待つ
        // Waits for a build command only before the first launch
        AwaitingBuildCommand = 1,
        // 電力供給に応じて建造する
        // Builds according to the supplied power
        Building = 2,
        // 初打ち上げ前だけ発射の合図を待つ
        // Waits for a launch command only before the first launch
        AwaitingLaunchCommand = 3,
        // 打ち上げが許され出力口の空きを待つ
        // Launch is permitted and waits for output space
        ReadyToLaunch = 4,
        // 消費済みのロケットが上昇する
        // The consumed rocket ascends
        Launching = 5,
    }
}
