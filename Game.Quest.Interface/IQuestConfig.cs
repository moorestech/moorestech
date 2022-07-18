using System.Collections.Generic;

namespace Game.Quest.Interface
{
    public interface IQuestConfig
    {
        public IReadOnlyList<QuestConfigData> GetAllQuestConfig();
        public QuestConfigData GetQuestConfig(string id);
        public List<string> GetQuestIds(string modId);
    }
}