using Game.Quest.Interface;
using Game.Quest.QuestEntity;

namespace Game.Quest.Factory.QuestTemplate
{
    public class ItemCraftQuestTemplate : IQuestTemplate
    {
        public IQuest CreateQuest(QuestConfigData questConfig)
        {
            return new ItemCraftQuest(questConfig);
        }

        public IQuest LoadQuest(QuestConfigData questConfig, bool isCompleted, bool isRewarded)
        {
            return new ItemCraftQuest(questConfig,isCompleted,isRewarded);
        }
    }
}