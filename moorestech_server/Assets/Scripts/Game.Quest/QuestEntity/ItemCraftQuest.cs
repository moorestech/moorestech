using System;
using System.Collections.Generic;
using Game.PlayerInventory.Interface.Event;
using Game.Quest.Interface;
using UnityEngine;

namespace Game.Quest.QuestEntity
{
    public class ItemCraftQuest : IQuest
    {
        private readonly int _questItemId;


        public ItemCraftQuest(QuestConfigData quest,  int questItemId,
            List<IQuest> preRequestQuests)
        {
            QuestConfig = quest;
            _questItemId = questItemId;
            PreRequestQuests = preRequestQuests;
        }

        public ItemCraftQuest(QuestConfigData quest,  bool isCompleted,
            bool isEarnedReward, int questItemId, List<IQuest> preRequestQuests)
            : this(quest,  questItemId, preRequestQuests)
        {
            IsCompleted = isCompleted;
            IsEarnedReward = isEarnedReward;
        }

        public QuestConfigData QuestConfig { get; }
        public bool IsCompleted { get; private set; }
        public bool IsEarnedReward { get; private set; }
        public IReadOnlyList<IQuest> PreRequestQuests { get; }
        public event Action<QuestConfigData> OnQuestCompleted;

        public void LoadQuestData(SaveQuestData saveQuestData)
        {
            if (saveQuestData.QuestId != QuestConfig.QuestId)
                //TODO ログ基盤に入れる
                throw new ArgumentException("ロードすべきクエストIDが一致しません");
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
            Debug.Log("You haven't completed this quest yet");
        }

        private void OnItemCraft((int itemId, int itemCount) result)
        {
            if (IsCompleted || result.itemId != _questItemId) return;

            IsCompleted = true;
            OnQuestCompleted?.Invoke(QuestConfig);
        }
    }
}