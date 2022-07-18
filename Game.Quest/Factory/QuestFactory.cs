using System;
using System.Collections.Generic;
using Core.Item;
using Game.PlayerInventory.Interface.Event;
using Game.Quest.Factory.QuestTemplate;
using Game.Quest.Interface;

namespace Game.Quest.Factory
{
    public class QuestFactory
    {
        private readonly IQuestConfig _questConfig;
        private readonly Dictionary<string,IQuestTemplate> _questTemplates =　new(); 

        public QuestFactory(IQuestConfig questConfig,ItemStackFactory itemStackFactory,ICraftingEvent craftingEvent)
        {
            //クエストのテンプレート一覧の作成
            _questTemplates.Add(VanillaQuestTypes.ItemCraftQuestType,new ItemCraftQuestTemplate(itemStackFactory,craftingEvent));
            
            _questConfig = questConfig;
        }

        public IQuest CreateQuest(string questId)
        {
            var quest = _questConfig.GetQuestConfig(questId);
            
            if (_questTemplates.ContainsKey(quest.QuestType))
            {
                return _questTemplates[quest.QuestType].CreateQuest(quest);
            }
            
            //TODO ログ取得基盤に入れるようにする
            throw new ArgumentException("[QuestFactory]指定されたクエストタイプ:"+quest.QuestType + "は存在しません。");
        }

        public IQuest LoadQuest(SaveQuestData loadedQuest)
        {
            var quest = _questConfig.GetQuestConfig(loadedQuest.QuestId);
            
            if (_questTemplates.ContainsKey(quest.QuestType))
            {
                return _questTemplates[quest.QuestType].LoadQuest(quest,loadedQuest.IsCompleted,loadedQuest.IsRewarded);
            }
            
            //TODO ログ取得基盤に入れるようにする
            throw new ArgumentException("[QuestFactory]指定されたクエストタイプ:"+quest.QuestType + "は存在しません。");
        }
    }
}