using System;
using System.Collections.Generic;
using Game.Quest.Factory.QuestTemplate;
using Game.Quest.Interface;

namespace Game.Quest.Factory
{
    public class QuestFactory
    {
        private readonly IQuestConfig _questConfig;
        private Dictionary<string,ItemCraftQuestTemplate> QuestTemplates =　new(); 

        public QuestFactory(IQuestConfig questConfig)
        {
            //クエストのテンプレート一覧の作成
            QuestTemplates.Add(VanillaQuestTypes.ItemCraftQuestType,new ItemCraftQuestTemplate());
            
            _questConfig = questConfig;
        }

        public IQuest CreateQuest(string questId)
        {
            var quest = _questConfig.GetQuestConfig(questId);
            
            if (QuestTemplates.ContainsKey(quest.QuestType))
            {
                return QuestTemplates[quest.QuestType].CreateQuest(quest);
            }
            
            //TODO ログ取得基盤に入れるようにする
            throw new ArgumentException("[QuestFactory]指定されたクエストタイプ:"+quest.QuestType + "は存在しません。");
        }
    }
}