using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Challenge
{
    public class ChallengeJsonObject
    {
        [JsonProperty("playerId")] public int PlayerId;
        [JsonProperty("completedIds")] public List<int> CompletedIds;
    }
}