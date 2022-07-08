using System;

namespace Game.Quest.Interface
{
    public interface IQuest
    {
        QuestConfigData Quest { get; }
        
        bool IsCompleted { get; }
        bool AcquiredReward { get; }
        event Action OnQuestCompleted;
    }
}