using System;
using Game.Quest.Interface;

namespace Game.Quest.QuestEntity
{
    public class ItemCraftQuest : IQuest
    {
        public QuestConfigData Quest { get; }
        public bool IsCompleted { get; private set; }
        public bool IsRewarded { get;  private set; }
        public event Action OnQuestCompleted;
        
        public ItemCraftQuest(QuestConfigData quest)
        {
            Quest = quest;
        }
        public ItemCraftQuest(QuestConfigData quest,bool isCompleted, bool isRewarded)
        {
            Quest = quest;
            IsCompleted = isCompleted;
            IsRewarded = isRewarded;
        }
    }
}