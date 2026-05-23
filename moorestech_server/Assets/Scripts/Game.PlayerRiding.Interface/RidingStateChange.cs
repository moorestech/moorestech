namespace Game.PlayerRiding.Interface
{
    public enum RidingStateChangeType : byte
    {
        Ride,
        Dismount,
    }

    // 乗車状態変化の通知データ
    // Notification data for riding state changes.
    public readonly struct RidingStateChange
    {
        public int PlayerId { get; }
        public RidingStateChangeType ChangeType { get; }
        public RidingState State { get; }
        public bool IsDismount => ChangeType == RidingStateChangeType.Dismount;
        
        public RidingStateChange(int playerId, RidingStateChangeType changeType, RidingState state)
        {
            PlayerId = playerId;
            ChangeType = changeType;
            State = state;
        }
    }
}
