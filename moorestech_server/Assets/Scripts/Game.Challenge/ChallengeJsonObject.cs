using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Challenge
{
    public class ChallengeJsonObject
    {
        [JsonProperty("completedGuids")] public List<string> CompletedGuids;
        [JsonProperty("currentChallengeGuids")] public List<string> CurrentChallengeGuids;
        [JsonProperty("playedSkitIds")] public List<string> PlayedSkitIds;
    }
}