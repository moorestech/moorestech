namespace Game.SaveLoad.Json
{
    // プレイヤーIDを持つセーブ節の正本。節名・キー名・重複許容をここだけで決める
    // The single source for save sections that carry a player id: section name, key name and duplicate tolerance
    internal static class PlayerScopedSaveSections
    {
        internal const string PlayerInventory = "playerInventory";
        internal const string PlayerRidingStates = "playerRidingStates";
        internal const string HotbarAssignments = "hotbarAssignments";
        internal const string RemainingPlacementCounts = "remainingPlacementCounts";
        internal const string ConstructionPayers = "constructionPayers";
        internal const string MiningCooldowns = "miningCooldowns";
        internal const string Entities = "entities";

        internal const string PlayerIdKey = "PlayerId";
        internal const string MiningCooldownPlayerIdKey = "playerId";
        internal const string EntityInstanceIdKey = "InstanceId";

        // entities はプレイヤー以外も混ざるため、この Type だけを対象にする
        // The entities section mixes other kinds, so only this Type participates
        internal const string PlayerEntityType = "va:Player";

        internal readonly struct Section
        {
            internal readonly string Name;
            internal readonly string Key;
            internal readonly bool AllowDuplicateIds;

            internal Section(string name, string key, bool allowDuplicateIds)
            {
                Name = name;
                Key = key;
                AllowDuplicateIds = allowDuplicateIds;
            }
        }

        // entities は判定条件が違うため表には含めず、利用側が別に扱う
        // The entities section has a different predicate, so it stays out of the table and callers handle it apart
        internal static readonly Section[] All =
        {
            new(PlayerInventory, PlayerIdKey, false),
            new(PlayerRidingStates, PlayerIdKey, false),
            new(HotbarAssignments, PlayerIdKey, false),
            new(RemainingPlacementCounts, PlayerIdKey, false),
            new(ConstructionPayers, PlayerIdKey, true),
            new(MiningCooldowns, MiningCooldownPlayerIdKey, false),
        };
    }
}
