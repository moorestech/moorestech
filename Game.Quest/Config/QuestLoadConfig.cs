using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Core.Util;
using Game.Quest.Interface;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Game.Quest.Config
{
    public class QuestLoadConfig
    {
        public (Dictionary<string, List<string>> ModIdToQuest, Dictionary<string, List<QuestConfigData>> QuestConfigs)
            LoadConfig(Dictionary<string,string> blockJsons,List<string> mods)
        {
        }


        private (Dictionary<string, List<string>> ModIdToQuest, Dictionary<string, List<QuestConfigData>> QuestConfigs) Load(Dictionary<string,string> questJsons,List<string> mods)
        {
            
            var modIdToQuest = new Dictionary<string, List<string>>();
            var questConfigs = new Dictionary<string, List<QuestConfigData>>();
            foreach (var questJson in questJsons)
            {
                var modId = questJson.Key;
                var loadedQuests = JsonConvert.DeserializeObject<QuestConfigJsonData[]>(questJson.Value);
                if (loadedQuests == null)
                {
                    Console.WriteLine("[Log] ModId:" + modId + "のクエストコンフィグのロードに失敗しました。");
                    continue;
                }

                var resultQuests = new List<QuestConfigData>();
                foreach (var quest in loadedQuests)
                {
                    
                    resultQuests.Add(questData);
                }
                questConfigs.Add(modId,resultQuests);
            }
        }

        /// <summary>
        /// JSONからQuestConfigJsonDataをロードする
        /// </summary>
        private (
            Dictionary<string, QuestConfigJsonData> KeyQuestIdConfigs,
            Dictionary<string, List<QuestConfigJsonData>> KeyModIdConfigs) LoadJsonToQuestConfigJsonData(Dictionary<string,string> questJsons)
        {
            var keyQuestIdConfigs = new Dictionary<string, QuestConfigJsonData>();
            var keyModIdConfigs = new Dictionary<string, List<QuestConfigJsonData>>();
            
            foreach (var questJson in questJsons)
            {
                var modId = questJson.Key;
                //クエストのロード
                var loadedQuests = JsonConvert.DeserializeObject<QuestConfigJsonData[]>(questJson.Value);
                if (loadedQuests == null)
                {
                    Console.WriteLine("[Log] ModId:" + modId + "のクエストコンフィグのロードに失敗しました。JSONファイルを確認してください。");
                    continue;
                }

                //辞書に格納
                var questList = loadedQuests.ToList();
                keyModIdConfigs.Add(modId,questList);
                foreach (var quest in questList)
                {
                    keyQuestIdConfigs.Add(quest.Id,quest);
                }
                
            }

            return (keyQuestIdConfigs, keyModIdConfigs);
        }
        
    }
    

    [JsonObject("SpaceAssets")]
    internal class QuestConfigJsonData
    {
        [JsonProperty("Id")]
        public string Id;
        [JsonProperty("Prerequisite")]
        public string[] Prerequisite;
        [JsonProperty("PrerequisiteType")]
        [JsonConverter(typeof(StringEnumConverter))]
        public QuestPrerequisiteType PrerequisiteType;
        [JsonProperty("Category")]
        public string Category;
        [JsonProperty("Type")]
        public string Type;
        [JsonProperty("Name")]
        public string Name;
        [JsonProperty("Description")]
        public string Description;
        [JsonProperty("UIPosX")]
        public float UiPosX;
        [JsonProperty("UIPosY")]
        public float UiPosY;
        [JsonProperty("RewardItem")]
        public int[,] RewardItem;
        [JsonProperty("Param")]
        public string Param;
    }



    static class QuestLoadExtension
    {
        public static QuestConfigData ToQuestConfigData(this QuestConfigJsonData questConfigJsonData,string modId,List<QuestConfigData> prerequisiteQuests,ItemStackFactory itemStackFactory)
        {
            var rewardItems = new List<IItemStack>();
            for (int i = 0; i < questConfigJsonData.RewardItem.GetLength(0); i++)
            {
                var id = questConfigJsonData.RewardItem[i, 0];
                var count = questConfigJsonData.RewardItem[i, 1];
                rewardItems.Add(itemStackFactory.Create(id,count));
            }
            
            
            return new QuestConfigData(
                modId,
                questConfigJsonData.Id,
                prerequisiteQuests,
                questConfigJsonData.Category,
                questConfigJsonData.PrerequisiteType,
                questConfigJsonData.Type,
                questConfigJsonData.Name,
                questConfigJsonData.Description,
                new CoreVector2(questConfigJsonData.UiPosX, questConfigJsonData.UiPosY),
                rewardItems,
                questConfigJsonData.Param);
        }
    }
}