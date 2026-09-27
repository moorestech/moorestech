using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.PlayerIdentity
{
    // セーブの players 節。身元とプレイヤーIDの対応・次のID・結びつけ候補
    // The save's players section: identity-to-id entries, the next id, and the claim candidate
    public class PlayersSaveJsonObject
    {
        [JsonProperty("nextPlayerId")] public int NextPlayerId;
        [JsonProperty("claimCandidatePlayerId")] public int? ClaimCandidatePlayerId;
        [JsonProperty("entries")] public List<PlayerIdentityEntryJsonObject> Entries;

        public PlayersSaveJsonObject(int nextPlayerId, int? claimCandidatePlayerId, List<PlayerIdentityEntryJsonObject> entries)
        {
            NextPlayerId = nextPlayerId;
            ClaimCandidatePlayerId = claimCandidatePlayerId;
            Entries = entries;
        }
    }

    // 身元が null のエントリは持ち主未定のプレイヤー
    // An entry whose identity is null is an unclaimed player
    public class PlayerIdentityEntryJsonObject
    {
        [JsonProperty("playerId")] public int PlayerId;
        [JsonProperty("identity")] public string Identity;

        public PlayerIdentityEntryJsonObject(int playerId, string identity)
        {
            PlayerId = playerId;
            Identity = identity;
        }
    }
}
