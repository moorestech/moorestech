using System.Collections.Generic;
using Game.Quest.Interface;

namespace Game.Quest.Factory
{
    public class QuestFactory
    {
        private readonly IQuestConfig _questConfig;
        private Dictionary<string,IQuest> QuestTemplates = 

        public QuestFactory(IQuestConfig questConfig)
        {
            _questConfig = questConfig;
        }

        public IQuest CreateQuest(string questId)
        {
            var quest = _questConfig.GetQuestConfig(questId);
            
        }
    }
}