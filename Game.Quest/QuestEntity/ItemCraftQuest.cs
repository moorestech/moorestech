using System;
using Core.Item;
using Game.PlayerInventory.Interface.Event;
using Game.Quest.Interface;
using Newtonsoft.Json;

namespace Game.Quest.QuestEntity
{
    public class ItemCraftQuest : IQuest
    {
        public QuestConfigData Quest { get; }
        public bool IsCompleted { get; private set; }
        public bool IsEarnedReward { get;  private set; }
        public event Action<QuestConfigData> OnQuestCompleted;

        private readonly int _questItemId;
        
        public ItemCraftQuest(QuestConfigData quest,ICraftingEvent craftingEvent, int questItemId)
        {
            Quest = quest;
            _questItemId = questItemId;
            craftingEvent.Subscribe(OnItemCraft);
        }
        public ItemCraftQuest(QuestConfigData quest,ICraftingEvent craftingEvent,bool isCompleted, bool isEarnedReward, int questItemId)
            :this(quest,craftingEvent,questItemId)
        {
            IsCompleted = isCompleted;
            IsEarnedReward = isEarnedReward;
        }

        private void OnItemCraft((int itemId, int itemCount) result)
        {
            if (IsCompleted || result.itemId != _questItemId) return;
            
            IsCompleted = true;
            OnQuestCompleted?.Invoke(Quest);
        }
        
        public void EarnReward()
        {
            if (IsCompleted)
            {
                IsEarnedReward = true;
                return;
            }
            //TODO ログ基盤に入れる
            Console.WriteLine("You haven't completed this quest yet");
        }
    }
}