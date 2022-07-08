using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Quest.Interface
{
    public class SaveQuestData
    {
        [JsonProperty("quests")]  public Dictionary<int, SaveOnePlayerQuests> QuestsData;
    }

    public class SaveOnePlayerQuests
    {
        [JsonProperty("id")] public string QuestId;
        
        [JsonProperty("co")] public bool IsCompleted;
        [JsonProperty("re")] public bool AcquiredReward;   
    }
}