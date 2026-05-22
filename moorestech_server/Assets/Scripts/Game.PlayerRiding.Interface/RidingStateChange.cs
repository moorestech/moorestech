namespace Game.PlayerRiding.Interface
{
    // 乗車状態変化の種別。Ride は乗車または復帰、Dismount は降車を表す。
    // Riding-state change kind. Ride means mounted/restored, Dismount means dismounted.
    public enum RidingStateChangeType : byte
    {
        Ride,
        Dismount,
    }

    // 乗車状態変化の通知データ。ChangeType で乗車/降車を明示する。
    // Notification payload for a riding-state change. ChangeType explicitly identifies ride/dismount.
    public readonly struct RidingStateChange
    {
        public RidingStateChange(int playerId, RidingStateChangeType changeType, RidingState state)
        {
            PlayerId = playerId;
            ChangeType = changeType;
            State = state;
        }

        public int PlayerId { get; }
        public RidingStateChangeType ChangeType { get; }
        public RidingState State { get; }
        public bool IsDismount => ChangeType == RidingStateChangeType.Dismount;
    }
}
