using System.Collections.Generic;

namespace Game.Quest.Interface
{
    public interface IQuestDataStore
    {
        public IReadOnlyList<IQuest> GetPlayerQuestProgress(int playerId);
        public Dictionary<int, SaveQuestData> GetQuestDataDictionary();
        public void LoadQuestDataDictionary(Dictionary<int, SaveQuestData> quests);
    }
}