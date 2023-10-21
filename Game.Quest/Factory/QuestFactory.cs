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


        ///     

        /// <param name="questConfigData"></param>
        /// <param name="allQuests"></param>
        /// <returns></returns>
        private List<IQuest> GetPreRequestQuest(QuestConfigData questConfigData, Dictionary<string, IQuest> allQuests)
        {
            
            if (questConfigData.PrerequisiteQuests.Count == 0) return new List<IQuest>();
            
            var preRequestQuests = new List<IQuest>();
            foreach (var preRequestConfig in questConfigData.PrerequisiteQuests)
            {
                if (allQuests.TryGetValue(preRequestConfig.QuestId, out var createdQuest))
                {
                    preRequestQuests.Add(createdQuest);
                    continue;
                }

                
                var quest = CreateQuest(preRequestConfig, GetPreRequestQuest(preRequestConfig, allQuests));
                allQuests.Add(quest.QuestConfig.QuestId, quest);
                preRequestQuests.Add(quest);
            }

            return preRequestQuests;
        }

        private IQuest CreateQuest(QuestConfigData questConfig, List<IQuest> preRequestQuest)
        {
            if (_questTemplates.ContainsKey(questConfig.QuestType)) return _questTemplates[questConfig.QuestType].CreateQuest(questConfig, preRequestQuest);
            //TODO 
            throw new ArgumentException("[QuestFactory]:" + questConfig.QuestType + "。");
        }

        public List<IQuest> LoadQuests(List<SaveQuestData> loadedQuests)
        {
            var newQuests = CreateQuestDictionary();
            foreach (var quest in loadedQuests)
            {
                
                if (newQuests.TryGetValue(quest.QuestId, out var newQuest))
                {
                    newQuest.LoadQuestData(quest);
                    continue;
                }

                //TODO 
                throw new ArgumentException("[QuestFactory]ID:" + quest.QuestId + "。");
            }

            return newQuests.Values.ToList();
        }
    }
}