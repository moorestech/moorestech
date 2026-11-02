namespace Game.Block.Interface.Component
{
    /// <summary>
    ///     クリーンルーム機械の稼働可否と、止まっている理由
    ///     Whether a clean-room machine may run, and why it is halted
    /// </summary>
    public enum CleanRoomOperability
    {
        NotEnclosed,
        ClassOut,
        Operable,
    }
}
