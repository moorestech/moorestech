using System.Collections.Generic;
using System.Linq;
using Game.Quest.Factory;
using Game.Quest.Interface;

namespace Game.Quest
{
    public class QuestDatastore : IQuestDataStore
    {
        private readonly IQuestConfig _questConfig;
        private readonly QuestFactory _questFactory;

        private readonly Dictionary<int, List<IQuest>> _quests = new();

        public QuestDatastore(IQuestConfig questConfig, QuestFactory questFactory)
        {
            _questConfig = questConfig;
            _questFactory = questFactory;
        }


        public IReadOnlyList<IQuest> GetPlayerQuestProgress(int playerId)
        {
            if (_quests.ContainsKey(playerId))
            {
                return _quests[playerId];
            }

            var newQuests =
                _questConfig.GetAllQuestConfig().
                    Select(q => _questFactory.CreateQuest(q.QuestId)).ToList();
            _quests.Add(playerId,newQuests);
            
            return newQuests;
        }

        public Dictionary<int,List<SaveQuestData>> GetQuestDataDictionary()
        {
            var saveData = new Dictionary<int, List<SaveQuestData>>();
            foreach (var quest in _quests)
            {
                saveData.Add(quest.Key,quest.Value.Select(q => q.ToSaveData()).ToList());
            }

            return saveData;
        }

        public void LoadQuestDataDictionary(Dictionary<int, List<SaveQuestData>> quests)
        {
            foreach (var questsList in quests)
            {
                //今あるクエストが全てロードずみかどうかチェックする辞書
                var allQuests = _questConfig.GetAllQuestConfig().ToDictionary(q => q.QuestId, q => false);
                var loadedQuestList = new List<IQuest>();
                foreach (var quest in questsList.Value)
                {
                    if (allQuests.ContainsKey(quest.QuestId))
                    {
                        loadedQuestList.Add(_questFactory.LoadQuest(quest.QuestId,quest.IsCompleted,quest.AcquiredReward));
                        continue;
                    }
                }
            }
            
        }
    }

    static class QuestExtension 
    {
        public static SaveQuestData ToSaveData(this IQuest quest)
        {
            return new SaveQuestData(quest.Quest.QuestId,quest.IsCompleted,quest.AcquiredReward);
        }
    }
}