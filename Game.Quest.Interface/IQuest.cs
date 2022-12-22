using System;
using System.Collections.Generic;

namespace Game.Quest.Interface
{
    public interface IQuest
    {
        QuestConfigData Quest { get; }
        
        bool IsCompleted { get; }
        bool IsEarnedReward { get; }
        IReadOnlyList<IQuest> PreRequestQuests { get; }
        event Action<QuestConfigData> OnQuestCompleted;

        public void LoadQuestData(SaveQuestData saveQuestData);
        public void EarnReward();
    }
}