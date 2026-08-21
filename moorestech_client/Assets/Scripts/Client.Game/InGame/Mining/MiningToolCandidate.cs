using Core.Master;

namespace Client.Game.InGame.Mining
{
    /// <summary>
    ///     採掘対象から解決した使用ツール
    ///     Tool candidate resolved by a mining target
    /// </summary>
    public readonly struct MiningToolCandidate
    {
        public readonly ItemId ToolItemId;
        public readonly float AttackSpeed;

        public MiningToolCandidate(ItemId toolItemId, float attackSpeed)
        {
            ToolItemId = toolItemId;
            AttackSpeed = attackSpeed;
        }
    }
}
