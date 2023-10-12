using Newtonsoft.Json;

namespace Game.Quest.Interface
{
    public class SaveQuestData
    {
        [JsonProperty("co")] public bool IsCompleted;
        [JsonProperty("re")] public bool IsRewarded;
        [JsonProperty("id")] public string QuestId;

        public SaveQuestData(string questId, bool isCompleted, bool isRewarded)
        {
            QuestId = questId;
            IsCompleted = isCompleted;
            IsRewarded = isRewarded;
        }
    }
}