using System;
using System.Collections.Generic;
using Core.Item;
using Core.Util;
using Game.Quest.Interface;

namespace Game.Quest
{
    public interface IQuest
    {
        QuestConfigData Quest { get; }
        public string QuestType { get; }
        
        bool IsCompleted { get; }
        bool AcquiredReward { get; }
        event Action OnQuestCompleted;
    }
}