using Game.Quest.Interface;

namespace Game.Quest.Factory.QuestTemplate
{
    public interface IQuestTemplate
    {
        public IQuest CreateQuest(QuestConfigData questConfig);
        public IQuest LoadQuest(QuestConfigData questConfigData,bool isCompleted,bool isRewarded);
    }
}