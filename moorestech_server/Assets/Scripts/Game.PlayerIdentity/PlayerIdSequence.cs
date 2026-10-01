namespace Game.PlayerIdentity
{
    // プレイヤーIDの連番規則の所有者。基点・次番・妥当範囲はここだけが決める
    // The owner of the player-id sequence rule; origin, successor and valid range live only here
    public static class PlayerIdSequence
    {
        public const int First = 1;

        // 払い出し済みの最大IDの次を返す。欠番は再利用しない
        // Returns the id that follows the highest assigned one; gaps are never reused
        public static int NextAfter(int lastAssignedPlayerId)
        {
            return lastAssignedPlayerId + 1;
        }

        public static bool IsValid(long playerId)
        {
            return First <= playerId && playerId <= int.MaxValue;
        }
    }
}
