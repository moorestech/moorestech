namespace Game.PlayerRiding.Interface
{
    // 乗車状態変化の通知データ。State == null は降車を表す。
    // Notification payload for a riding-state change. State == null means dismounted.
    public readonly struct RidingStateChange
    {
        public RidingStateChange(int playerId, RidingState state)
        {
            PlayerId = playerId;
            State = state;
        }

        public int PlayerId { get; }
        public RidingState State { get; }
        public bool IsDismount => State == null;
    }
}
