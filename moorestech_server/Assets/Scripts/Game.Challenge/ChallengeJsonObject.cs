using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Challenge
{
    public class ChallengeJsonObject
    {
        [JsonProperty("completedGuids")] public List<string> CompletedGuids;
        [JsonProperty("playerId")] public int PlayerId;
        [JsonProperty("currentChallengeGuids")] public List<string> CurrentChallengeGuids;
    }
}