using System;
using System.Collections.Generic;
using System.Linq;
using Game.Quest.Event;
using Game.Quest.Factory;
using Game.Quest.Interface;
using Game.Quest.Interface.Event;

namespace Game.Quest
{
    public class QuestDatastore : IQuestDataStore
    {
        private readonly IQuestConfig _questConfig;
        private readonly QuestFactory _questFactory;
        private readonly QuestCompletedEvent _questCompletedEvent;

        private readonly Dictionary<int, List<IQuest>> _quests = new();

        public QuestDatastore(IQuestConfig questConfig, QuestFactory questFactory,IQuestCompletedEvent questCompletedEvent)
        {
            _questConfig = questConfig;
            _questFactory = questFactory;
            _questCompletedEvent = (QuestCompletedEvent)questCompletedEvent;
        }


        public IReadOnlyList<IQuest> GetPlayerQuestProgress(int playerId)
        {
            if (_quests.ContainsKey(playerId))
            {
                return _quests[playerId];
            }

            //新しいプレイヤーなので新しいクエストの作成
            var newQuests = _questFactory.CreateQuests();
                
            _quests.Add(playerId,newQuests);
            //クエスト完了時にイベントを発火させる
            SetPlayerEvent(playerId,newQuests);
            
            return newQuests;
        }

        public Dictionary<int,List<SaveQuestData>> GetQuestDataDictionary()
        {
            var saveData = new Dictionary<int, List<SaveQuestData>>();
            foreach (var quest in _quests)
            {
                saveData.Add(quest.Key,quest.Value.Select(q => q.ToSaveData()).ToList());
            }

            return saveData;
        }

        public IQuest GetQuestData(int playerId, string questId)
        {
            foreach (var quest in GetPlayerQuestProgress(playerId))
            {
                if (quest.QuestConfig.QuestId != questId) continue;
                
                return quest;
            }

            //TODO ログ基盤
            throw new ArgumentException("クエストがありませんでした QuestId:" + questId);
        }

        public void LoadQuestDataDictionary(Dictionary<int, List<SaveQuestData>> quests)
        {
            foreach (var playerToQuestsList in quests)
            {
                var playerId = playerToQuestsList.Key;
                var questList = _questFactory.LoadQuests(playerToQuestsList.Value);
                _quests.Add(playerId,questList);
                //クエスト完了時にイベントを発火させるために登録
                SetPlayerEvent(playerId,questList);
            }
        }

        private void SetPlayerEvent(int playerId,IReadOnlyList<IQuest> quests)
        {
            foreach (var quest in quests)
            {
                quest.OnQuestCompleted += q =>
                {
                    _questCompletedEvent.InvokeQuestCompleted(playerId, q.QuestId);
                };
            }
        }
    }

    static class QuestExtension 
    {
        public static SaveQuestData ToSaveData(this IQuest quest)
        {
            return new SaveQuestData(quest.QuestConfig.QuestId,quest.IsCompleted,quest.IsEarnedReward);
        }
    }
}