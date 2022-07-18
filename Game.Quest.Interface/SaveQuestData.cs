using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Quest.Interface
{
    public class SaveQuestData
    {
        [JsonProperty("id")] public string QuestId;
        
        [JsonProperty("co")] public bool IsCompleted;
        [JsonProperty("re")] public bool IsRewarded;

        public SaveQuestData(string questId, bool isCompleted, bool isRewarded)
        {
            QuestId = questId;
            IsCompleted = isCompleted;
            IsRewarded = isRewarded;
        }
    }
}