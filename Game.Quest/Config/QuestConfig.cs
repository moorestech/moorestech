using System;
using System.Collections.Generic;
using System.Linq;
using Core.ConfigJson;
using Core.Item;
using Game.Quest.Interface;

namespace Game.Quest.Config
{
    public class QuestConfig : IQuestConfig
    {
        private readonly Dictionary<string, List<string>> _modIdToQuests;
        private readonly Dictionary<string, QuestConfigData> _questIdToQuestConfigs;
        public QuestConfig(ConfigJsonList configJson,ItemStackFactory itemStackFactory)
        {
            (_modIdToQuests, _questIdToQuestConfigs) = QuestLoadConfig.LoadConfig(itemStackFactory,configJson.QuestConfigs);
        }

        public IReadOnlyList<QuestConfigData> GetAllQuestConfig()
        {
            return _questIdToQuestConfigs.Values.ToList();
        }

        public QuestConfigData GetQuestConfig(string id)
        {
            if (_questIdToQuestConfigs.TryGetValue(id,out var quest))
            {
                return quest;
            }

            //TODO ログ取得基盤に入れるようにする
            Console.WriteLine("[QuestConfig]指定された クエストID:"+id + "は存在しません。");
            return null;
        }

        public List<string> GetQuestIds(string modId)
        {
            if (_modIdToQuests.TryGetValue(modId,out var quests))
            {
                return quests;
            }

            //TODO ログ取得基盤に入れるようにする
            Console.WriteLine("[QuestConfig]指定された ModId:"+modId + "にクエストは存在しません。");
            return new List<string>();
        }

        public Dictionary<string, List<QuestConfigData>> GetQuestListEachCategory()
        {
            var questListEachCategory = new Dictionary<string, List<QuestConfigData>>();
            foreach (var quest in _questIdToQuestConfigs.Values)
            {
                if (!questListEachCategory.TryGetValue(quest.QuestCategory, out var questList))
                {
                    questList = new List<QuestConfigData>();
                    questListEachCategory.Add(quest.QuestCategory, questList);
                }
                questList.Add(quest);
            }
            return questListEachCategory;
        }
    }
}