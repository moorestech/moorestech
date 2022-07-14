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
        public bool IsRewarded { get;  private set; }
        public event Action OnQuestCompleted;
        
        private readonly int _questItemId;
        
        public ItemCraftQuest(QuestConfigData quest,ICraftingEvent craftingEvent, int questItemId)
        {
            Quest = quest;
            _questItemId = questItemId;
            craftingEvent.Subscribe(OnItemCraft);
        }
        public ItemCraftQuest(QuestConfigData quest,ICraftingEvent craftingEvent,bool isCompleted, bool isRewarded, int questItemId)
            :this(quest,craftingEvent,questItemId)
        {
            IsCompleted = isCompleted;
            IsRewarded = isRewarded;
        }

        private void OnItemCraft(IItemStack itemStack)
        {
            if (!IsCompleted && itemStack.Id != _questItemId) return;
            
            IsCompleted = true;
            OnQuestCompleted?.Invoke();
        }
    }
}