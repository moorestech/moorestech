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
                    Select(quest => _questFactory.CreateQuest(quest.QuestId)).ToList();
            _quests.Add(playerId,newQuests);
            
            return newQuests;
        }

    }
}