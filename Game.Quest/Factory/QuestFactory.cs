using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Game.PlayerInventory.Interface.Event;
using Game.Quest.Factory.QuestTemplate;
using Game.Quest.Interface;

namespace Game.Quest.Factory
{
    public class QuestFactory
    {
        private readonly IQuestConfig _questConfig;
        private readonly Dictionary<string, IQuestTemplate> _questTemplates =　new();

        public QuestFactory(IQuestConfig questConfig, ItemStackFactory itemStackFactory, ICraftingEvent craftingEvent)
        {
            //クエストのテンプレート一覧の作成
            _questTemplates.Add(VanillaQuestTypes.ItemCraftQuestType, new ItemCraftQuestTemplate(itemStackFactory, craftingEvent));

            _questConfig = questConfig;
        }

        public List<IQuest> CreateQuests()
        {
            return CreateQuestDictionary().Values.ToList();
        }

        private Dictionary<string, IQuest> CreateQuestDictionary()
        {
            var questConfigs = _questConfig.GetAllQuestConfig();
            var quests = new Dictionary<string, IQuest>();
            foreach (var questConfig in questConfigs)
            {
                var id = questConfig.QuestId;
                var newQuest = CreateQuest(questConfig, GetPreRequestQuest(questConfig, quests));
                quests.Add(id, newQuest);
            }

            return quests;
        }

        /// <summary>
        ///     再帰的にクエストを探索し、前提クエストを取得する
        /// </summary>
        /// <param name="questConfigData">前提クエストを作成したクエストコンフィグ</param>
        /// <param name="allQuests">この関数内で新たに作ったクエストを入れておくためのリスト</param>
        /// <returns></returns>
        private List<IQuest> GetPreRequestQuest(QuestConfigData questConfigData, Dictionary<string, IQuest> allQuests)
        {
            //前提クエストがないときはそのまま通す
            if (questConfigData.PrerequisiteQuests.Count == 0) return new List<IQuest>();
            //再帰的に前提クエストを作成する
            var preRequestQuests = new List<IQuest>();
            foreach (var preRequestConfig in questConfigData.PrerequisiteQuests)
            {
                if (allQuests.TryGetValue(preRequestConfig.QuestId, out var createdQuest))
                {
                    preRequestQuests.Add(createdQuest);
                    continue;
                }

                //既に作ったクエストになかったので作成してリストに入れる
                var quest = CreateQuest(preRequestConfig, GetPreRequestQuest(preRequestConfig, allQuests));
                allQuests.Add(quest.QuestConfig.QuestId, quest);
                preRequestQuests.Add(quest);
            }

            return preRequestQuests;
        }

        private IQuest CreateQuest(QuestConfigData questConfig, List<IQuest> preRequestQuest)
        {
            if (_questTemplates.ContainsKey(questConfig.QuestType)) return _questTemplates[questConfig.QuestType].CreateQuest(questConfig, preRequestQuest);
            //TODO ログ取得基盤に入れるようにする
            throw new ArgumentException("[QuestFactory]指定されたクエストタイプ:" + questConfig.QuestType + "は存在しません。");
        }

        public List<IQuest> LoadQuests(List<SaveQuestData> loadedQuests)
        {
            var newQuests = CreateQuestDictionary();
            foreach (var quest in loadedQuests)
            {
                //クエストが存在するときはそのクエストを取得する
                if (newQuests.TryGetValue(quest.QuestId, out var newQuest))
                {
                    newQuest.LoadQuestData(quest);
                    continue;
                }

                //TODO ログ取得基盤に入れるようにする
                throw new ArgumentException("[QuestFactory]クエストID:" + quest.QuestId + "が存在しません。");
            }

            return newQuests.Values.ToList();
        }
    }
}