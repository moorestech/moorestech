namespace Client.Game.InGame.Entity.Factory
{
    public sealed class BeltItemPrefabLoadResult
    {
        public BeltItemPrefab Prefab { get; }
        public string FailureReason { get; }
        public bool Succeeded => FailureReason == null;
        private BeltItemPrefabLoadResult(BeltItemPrefab prefab, string failureReason)
        { Prefab = prefab; FailureReason = failureReason; }
        public static BeltItemPrefabLoadResult Ready(BeltItemPrefab prefab) => new(prefab, null);
        public static BeltItemPrefabLoadResult Missing(string reason) => new(default, reason);
    }
}
