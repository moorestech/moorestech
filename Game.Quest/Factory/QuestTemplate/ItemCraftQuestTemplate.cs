using Game.Quest.Interface;

namespace Game.Quest.Factory.QuestTemplate
{
    public class ItemCraftQuestTemplate : IQuestTemplate
    {
        public IQuest CreateQuest(QuestConfigData questConfig)
        {
            return new ItemCraftQuest();
        }

        public IQuest LoadQuest(QuestConfigData questConfigData, bool isCompleted, bool isRewarded)
        {
            return new ItemCraftQuest(isCompleted,isRewarded);
        }
    }
}