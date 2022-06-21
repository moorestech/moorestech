using System.Collections.Generic;

namespace Game.Quest.Interface
{
    public interface IQuestConfig
    {
        public QuestConfigData GetQuestConfig(string id);
        public List<string> GetQuestIds(string modId);
    }
}