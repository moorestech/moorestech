namespace Game.Block.Interface.State
{
    // 拠点の今の状態。待ちの理由もここで表す。未接続は接続できるか（プールが判定）で2つに分かれる
    // The base's current state, including why it is waiting; unconnected splits by whether it can connect (judged by the pool)
    public enum SpaceElevatorBaseStatus
    {
        ReadyToConnect,
        InsufficientCable,
        WaitingSatellite,
        Climbing,
        LowPower,
        OutputBlocked,
        InletBlocked,
    }
}
