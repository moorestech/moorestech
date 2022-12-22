using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Game.PlayerInventory.Interface.Event;
using Game.Quest.Interface;
using Newtonsoft.Json;

namespace Game.Quest.QuestEntity
{
    public class ItemCraftQuest : IQuest
    {
        public QuestConfigData QuestConfig { get; }
        public bool IsCompleted { get; private set; }
        public bool IsEarnedReward { get;  private set; }
        public IReadOnlyList<IQuest> PreRequestQuests { get; private set; }
        public event Action<QuestConfigData> OnQuestCompleted;

        private readonly int _questItemId;


        public ItemCraftQuest(QuestConfigData quest,ICraftingEvent craftingEvent, int questItemId, List<IQuest> preRequestQuests)
        {
            QuestConfig = quest;
            _questItemId = questItemId;
            PreRequestQuests = preRequestQuests;
            craftingEvent.Subscribe(OnItemCraft);
        }
        public ItemCraftQuest(QuestConfigData quest,ICraftingEvent craftingEvent,bool isCompleted, bool isEarnedReward, int questItemId, List<IQuest> preRequestQuests)
            :this(quest,craftingEvent,questItemId, preRequestQuests)
        {
            IsCompleted = isCompleted;
            IsEarnedReward = isEarnedReward;
        }

        private void OnItemCraft((int itemId, int itemCount) result)
        {
            if (IsCompleted || result.itemId != _questItemId) return;
            
            IsCompleted = true;
            OnQuestCompleted?.Invoke(QuestConfig);
        }

        public void LoadQuestData(SaveQuestData saveQuestData)
        {
            if (saveQuestData.QuestId != QuestConfig.QuestId)
            {
                //TODO ログ基盤に入れる
                throw new ArgumentException("ロードすべきクエストIDが一致しません");
                    
            }
            IsCompleted = saveQuestData.IsCompleted;
            IsEarnedReward = saveQuestData.IsRewarded;
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