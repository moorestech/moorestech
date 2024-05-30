using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Challenge
{
    public class ChallengeJsonObject
    {
        [JsonProperty("completedIds")] public List<int> CompletedIds;
        [JsonProperty("playerId")] public int PlayerId;
    }
}