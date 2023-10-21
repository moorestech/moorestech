using System.Collections.Generic;

namespace Game.Quest.Interface
{
    public interface IQuestDataStore
    {
        public IReadOnlyList<IQuest> GetPlayerQuestProgress(int playerId);
        public Dictionary<int, List<SaveQuestData>> GetQuestDataDictionary();
        public IQuest GetQuestData(int playerId, string questId);


        ///     
        ///     Key ID Value 

        public void LoadQuestDataDictionary(Dictionary<int, List<SaveQuestData>> quests);
    }
}