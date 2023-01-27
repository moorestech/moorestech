using System.Collections.Generic;

namespace Game.Quest.Interface
{
    public interface IQuestDataStore
    {
        public IReadOnlyList<IQuest> GetPlayerQuestProgress(int playerId);
        public Dictionary<int, List<SaveQuestData>> GetQuestDataDictionary();
        public IQuest GetQuestData(int playerId,string questId);
        
        /// <summary>
        /// クエストのデータをロードします
        /// Key プレイヤーID Value クリア、報酬受け取り済みなどのクエストデータ
        /// </summary>
        public void LoadQuestDataDictionary(Dictionary<int, List<SaveQuestData>> quests);
    }
}