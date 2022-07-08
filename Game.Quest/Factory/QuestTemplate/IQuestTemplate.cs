using Game.Quest.Interface;

namespace Game.Quest.Factory.QuestTemplate
{
    public interface IQuestTemplate
    {
        public IQuest CreateQuest(IQuestConfig questConfig);
    }
}