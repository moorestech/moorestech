using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Quest.Interface
{
    public class SaveQuestData
    {
        [JsonProperty("id")] public string QuestId;
        
        [JsonProperty("co")] public bool IsCompleted;
        [JsonProperty("re")] public bool AcquiredReward;

        public SaveQuestData(string questId, bool isCompleted, bool acquiredReward)
        {
            QuestId = questId;
            IsCompleted = isCompleted;
            AcquiredReward = acquiredReward;
        }
    }
}