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
        public readonly string Identity;

        // 下見した時点の対応表の世代。別の確定が割り込んだ下見をCommitで弾くために持つ
        // The table generation at preview time, so Commit can reject a preview another commit invalidated
        internal readonly int Generation;

        internal PlayerIdAssignment(int playerId, PlayerIdAssignmentKind kind, string identity, int generation)
        {
            PlayerId = playerId;
            Kind = kind;
            Identity = identity;
            Generation = generation;
        }
    }
}
