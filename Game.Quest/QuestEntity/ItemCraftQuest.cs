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
        public QuestConfigData Quest { get; }
        public bool IsCompleted { get; private set; }
        public bool IsEarnedReward { get;  private set; }
        public event Action<QuestConfigData> OnQuestCompleted;

        private readonly List<IQuest> _preRequestQuests;

        private readonly int _questItemId;



        public bool IsRewardEarnable
        {
            get
            {
                //既に報酬を受け取ったのでfalse
                if (IsEarnedReward)
                {
                    return false;
                }
                //まだクエストを完了していないのでfalse
                if (!IsCompleted)
                {
                    return false;
                }
                //完了済みで前提クエストが無ければtrue
                if (_preRequestQuests.Count == 0)
                {
                    return true;
                }
                
                
                //前提クエストの完了しているクエスト数を取得
                var preRequestQuestCount = _preRequestQuests.Count(quest => quest.IsCompleted);

                switch (Quest.QuestPrerequisiteType)
                {
                    //AND条件ですべて完了していたらtrue
                    case QuestPrerequisiteType.And when preRequestQuestCount == _preRequestQuests.Count:
                    //OR条件でいずれか完了していたらtrue
                    case QuestPrerequisiteType.Or when preRequestQuestCount > 0:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public ItemCraftQuest(QuestConfigData quest,ICraftingEvent craftingEvent, int questItemId, List<IQuest> preRequestQuests)
        {
            Quest = quest;
            _questItemId = questItemId;
            _preRequestQuests = preRequestQuests;
            craftingEvent.Subscribe(OnItemCraft);
        }
        public ItemCraftQuest(QuestConfigData quest,ICraftingEvent craftingEvent,bool isCompleted, bool isEarnedReward, int questItemId, List<IQuest> prequests)
            :this(quest,craftingEvent,questItemId, prequests)
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

        public void LoadQuestData(SaveQuestData saveQuestData)
        {
            if (saveQuestData.QuestId != Quest.QuestId)
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