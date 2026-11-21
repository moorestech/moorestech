namespace Game.Block.Interface.State
{
    // 拠点の今の状態。待ちの理由もここで表す。未接続は接続できるか（プールが判定）で2つに分かれ、積荷の未選択はそれより先に出る
    // The base's current state, including why it is waiting; unconnected splits by connectability (judged by the pool), and an unselected cargo comes before both
    public enum SpaceElevatorBaseStatus
    {
        ReadyToConnect,
        InsufficientCable,
        WaitingSatellite,
        Climbing,
        LowPower,
        OutputBlocked,
        InletBlocked,
        CargoNotSelected,
    }
}
