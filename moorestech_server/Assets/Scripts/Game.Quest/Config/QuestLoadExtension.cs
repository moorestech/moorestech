using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Game.Quest.Interface;
using UnityEngine;

namespace Game.Quest.Config
{
    internal static class QuestLoadExtension
    {
        public static QuestConfigData ToQuestConfigData(this QuestConfigJsonData questConfigJsonData,
            List<QuestConfigData> prerequisiteQuests, ItemStackFactory itemStackFactory)
        {
            var rewardItems = questConfigJsonData.RewardItem
                .Select(i => itemStackFactory.Create(i.ModId, i.Name, i.Count)).ToList();


            return new QuestConfigData(
                questConfigJsonData.ModId,
                questConfigJsonData.QuestId,
                prerequisiteQuests,
                questConfigJsonData.Category,
                questConfigJsonData.PrerequisiteType,
                questConfigJsonData.Type,
                questConfigJsonData.Name,
                questConfigJsonData.Description,
                new Vector2(questConfigJsonData.UiPosX, questConfigJsonData.UiPosY),
                rewardItems,
                // パラメーターは " を ' にしたjsonデータなのでReplaceする
                questConfigJsonData.Param.Replace("'", "\""));
        }
    }
}