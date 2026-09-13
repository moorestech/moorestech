using Newtonsoft.Json;

namespace Game.Map.Interface.Json
{
    // プレイヤー1人分の手掘りクールダウン。tickを保存しないと、ロード直後の再生が保存前に拒否された採掘を通す
    // One player's hand-mining cooldown; without the saved tick a replay after load accepts mining the live world rejected
    public class PlayerMiningCooldownSaveJsonObject
    {
        [JsonProperty("playerId")] public int PlayerId { get; set; }
        [JsonProperty("lastAttackTick")] public ulong LastAttackTick { get; set; }

        public PlayerMiningCooldownSaveJsonObject(int playerId, ulong lastAttackTick)
        {
            PlayerId = playerId;
            LastAttackTick = lastAttackTick;
        }
    }
}
