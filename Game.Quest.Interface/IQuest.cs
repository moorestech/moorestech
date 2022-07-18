using System;

namespace Game.Quest.Interface
{
    public interface IQuest
    {
        QuestConfigData Quest { get; }
        
        bool IsCompleted { get; }
        bool IsEarnedReward { get; }
        event Action<QuestConfigData> OnQuestCompleted;
        
        public void EarnReward();
    }
}