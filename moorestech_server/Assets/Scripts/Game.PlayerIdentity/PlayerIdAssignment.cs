namespace Game.PlayerIdentity
{
    public enum PlayerIdAssignmentKind
    {
        Known,
        ClaimedCandidate,
        NewlyAssigned,
    }

    // ハンドシェイクでの採番結果
    // The id assignment made at handshake
    public readonly struct PlayerIdAssignment
    {
        public readonly int PlayerId;
        public readonly PlayerIdAssignmentKind Kind;

        public PlayerIdAssignment(int playerId, PlayerIdAssignmentKind kind)
        {
            PlayerId = playerId;
            Kind = kind;
        }
    }
}
