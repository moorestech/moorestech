using System;
using System.Collections.Generic;
using Core.Item;
using Core.Util;

namespace Game.Quest
{
    public interface IQuest
    {
        QuestParameter Quest { get; }
        public string QuestType { get; }
        
        bool IsCompleted { get; }
        bool AcquiredReward { get; }
        event Action OnQuestCompleted;
    }
}