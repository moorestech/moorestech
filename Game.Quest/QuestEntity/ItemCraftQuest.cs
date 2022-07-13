using System;
using Core.Item;
using Game.Quest.Interface;
using Newtonsoft.Json;

namespace Game.Quest.QuestEntity
{
    public class ItemCraftQuest : IQuest
    {
        public QuestConfigData Quest { get; }
        public bool IsCompleted { get; private set; }
        public bool IsRewarded { get;  private set; }
        public event Action OnQuestCompleted;
        
        private int _questItemId;
        
        public ItemCraftQuest(QuestConfigData quest, int questItemId)
        {
            Quest = quest;
            _questItemId = questItemId;
        }
        public ItemCraftQuest(QuestConfigData quest,bool isCompleted, bool isRewarded, int questItemId)
        {
            Quest = quest;
            IsCompleted = isCompleted;
            IsRewarded = isRewarded;
            _questItemId = questItemId;
        }
    }
}