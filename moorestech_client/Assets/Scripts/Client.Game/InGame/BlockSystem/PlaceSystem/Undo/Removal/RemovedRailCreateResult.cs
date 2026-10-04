namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal
{
    /// <summary>
    ///     レール区間を撤去記録にした結果。作れなかった理由を区別する（FreeSegmentは設計上の対象外、NodeNotSyncedは記録不能）
    ///     Result of turning a rail edge into a removal record; distinguishes why it was not created (FreeSegment is out of scope by design, NodeNotSynced is unrecordable)
    /// </summary>
    public enum RemovedRailCreateOutcome
    {
        Created,
        NodeNotSynced,
        StationInternal,
        FreeSegment,
    }

    public readonly struct RemovedRailCreateResult
    {
        public RemovedRailCreateOutcome Outcome { get; }
        public RemovedRail Rail { get; }

        private RemovedRailCreateResult(RemovedRailCreateOutcome outcome, RemovedRail rail)
        {
            Outcome = outcome;
            Rail = rail;
        }

        public static RemovedRailCreateResult Created(RemovedRail rail)
        {
            return new RemovedRailCreateResult(RemovedRailCreateOutcome.Created, rail);
        }

        public static RemovedRailCreateResult NotCreated(RemovedRailCreateOutcome outcome)
        {
            return new RemovedRailCreateResult(outcome, null);
        }
    }
}
