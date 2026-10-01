namespace Client.Game.InGame.Entity.Factory
{
    public sealed class BeltItemCreationResult
    {
        public IEntityObject View { get; }
        public string FailureReason { get; }
        public bool Succeeded => FailureReason == null;
        private BeltItemCreationResult(IEntityObject view, string failureReason)
        { View = view; FailureReason = failureReason; }
        public static BeltItemCreationResult Created(IEntityObject view) => new(view, null);
        public static BeltItemCreationResult Missing(string reason) => new(null, reason);
    }
}
