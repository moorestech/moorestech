using System.Collections.Generic;
using Game.Quest.Interface;

namespace Game.Quest.Factory.QuestTemplate
{
    public interface IQuestTemplate
    {

        public IQuest CreateQuest(QuestConfigData questConfig, List<IQuest> preRequestQuests);

        public IQuest LoadQuest(QuestConfigData questConfig, bool isCompleted, bool isRewarded, List<IQuest> preRequestQuests);
    }
}