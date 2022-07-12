using System;

namespace Game.Quest.Interface
{
    public interface IQuest
    {
        QuestConfigData Quest { get; }
        
        bool IsCompleted { get; }
        bool IsRewarded { get; }
        event Action OnQuestCompleted;
    }
}