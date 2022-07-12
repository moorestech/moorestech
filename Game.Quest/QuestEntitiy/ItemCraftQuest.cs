using System;
using System.Collections.Generic;
using Core.Item;
using Core.Util;
using Game.Quest.Interface;

namespace Game.Quest
{
    public class ItemCraftQuest : IQuest
    {
        public QuestConfigData Quest { get; }
        public bool IsCompleted { get; private set; }
        public bool IsRewarded { get;  private set; }
        public event Action OnQuestCompleted;
        
        public ItemCraftQuest() { }
        public ItemCraftQuest(bool isCompleted, bool isRewarded)
        {
            IsCompleted = isCompleted;
            IsRewarded = isRewarded;
        }
    }
}