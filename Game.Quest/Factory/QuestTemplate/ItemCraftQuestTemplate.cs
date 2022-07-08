using Game.Quest.Interface;

namespace Game.Quest.Factory.QuestTemplate
{
    public class ItemCraftQuestTemplate : IQuestTemplate
    {
        public IQuest CreateQuest(IQuestConfig questConfig)
        {
            return new ItemCraftQuest();
        }
    }
}