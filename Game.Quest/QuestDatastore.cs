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
            var newQuests =
                _questConfig.GetAllQuestConfig().Select(q => _questFactory.CreateQuest(q.QuestId)).ToList();
                
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

        public void LoadQuestDataDictionary(Dictionary<int, List<SaveQuestData>> quests)
        {
            foreach (var playerToQuestsList in quests)
            {
                var allQuests = 
                    _questConfig.GetAllQuestConfig().ToDictionary(q => q.QuestId, q => _questFactory.CreateQuest(q.QuestId));
                
                foreach (var loadedQuest in playerToQuestsList.Value.Where(q => allQuests.ContainsKey(q.QuestId)))
                {
                    allQuests[loadedQuest.QuestId] = _questFactory.LoadQuest(loadedQuest);
                }

                var playerId = playerToQuestsList.Key;
                var questList = allQuests.Values.ToList();
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
            return new SaveQuestData(quest.Quest.QuestId,quest.IsCompleted,quest.IsEarnedReward);
        }
    }
}