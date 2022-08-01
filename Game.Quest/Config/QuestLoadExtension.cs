using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Core.Util;
using Game.Quest.Interface;

namespace Game.Quest.Config
{
    static class QuestLoadExtension
    {
        public static QuestConfigData ToQuestConfigData(this QuestConfigJsonData questConfigJsonData,List<QuestConfigData> prerequisiteQuests,ItemStackFactory itemStackFactory)
        {
            var rewardItems = questConfigJsonData.RewardItem.Select(i => itemStackFactory.Create(i.ModId, i.Name, i.Count)).ToList();


            return new QuestConfigData(
                questConfigJsonData.ModId,
                questConfigJsonData.QuestId,
                prerequisiteQuests,
                questConfigJsonData.Category,
                questConfigJsonData.PrerequisiteType,
                questConfigJsonData.Type,
                questConfigJsonData.Name,
                questConfigJsonData.Description,
                new CoreVector2(questConfigJsonData.UiPosX, questConfigJsonData.UiPosY),
                rewardItems,
                // パラメーターは " を ' にしたjsonデータなのでReplaceする
                questConfigJsonData.Param.Replace("'", "\""));
        }
    }
}