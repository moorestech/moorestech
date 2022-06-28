using System.Collections.Generic;
using Core.ConfigJson;
using Game.Quest.Interface;

namespace Game.Quest
{
    public class QuestConfig : IQuestConfig
    {
        public QuestConfig(ConfigJsonList configJson)
        {
            
        }

        public QuestConfigData GetQuestConfig(string id)
        {
            throw new System.NotImplementedException();
        }

        public List<string> GetQuestIds(string modId)
        {
            throw new System.NotImplementedException();
        }
    }
}