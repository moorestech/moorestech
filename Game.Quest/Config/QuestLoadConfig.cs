using System;
using System.Collections.Generic;
using Core.Item;
using Game.Quest.Interface;
using Newtonsoft.Json;

namespace Game.Quest.Config
{
    /// <summary>
    ///     
    ///     
    /// </summary>
    internal static class QuestLoadConfig
    {
        public static (Dictionary<string, List<string>> ModIdToQuests, Dictionary<string, QuestConfigData> QuestIdToQuestConfigs) LoadConfig(ItemStackFactory itemStackFactory, Dictionary<string, string> blockJsons)
        {
            var questIdConfig = CreateQuestIdConfig(itemStackFactory, blockJsons);
            var modIdToQuests = CreateModIdToQuestId(questIdConfig);

            return (modIdToQuests, questIdConfig);
        }

        private static Dictionary<string, QuestConfigData> CreateQuestIdConfig(ItemStackFactory itemStackFactory, Dictionary<string, string> blockJsons)
        {
            Dictionary<string, QuestConfigData> alreadyMadeConfigs = new();
            var jsonQuestConfig = LoadJsonToQuestConfigJsonData(blockJsons);

            //JSON
            foreach (var jsonConfig in jsonQuestConfig.Values)
            {
                if (alreadyMadeConfigs.ContainsKey(jsonConfig.QuestId)) continue;

                
                var prerequisiteQuests = AssemblyPrerequisiteQuests(itemStackFactory, jsonConfig, new List<string>(), alreadyMadeConfigs, jsonQuestConfig);

                //（true）
                if (alreadyMadeConfigs.ContainsKey(jsonConfig.QuestId)) continue;
                alreadyMadeConfigs.Add(jsonConfig.QuestId, jsonConfig.ToQuestConfigData(prerequisiteQuests, itemStackFactory));
            }

            return alreadyMadeConfigs;
        }



        ///     

        /// <param name="itemStackFactory">itemStackFactory</param>
        /// <param name="questConfigJsonData">json</param>
        /// <param name="detectLoopLog">。。</param>
        /// <param name="alreadyMadeConfigs">。</param>
        /// <param name="keyQuestIdJsonConfigs"></param>
        /// <returns></returns>
        private static List<QuestConfigData> AssemblyPrerequisiteQuests(ItemStackFactory itemStackFactory, QuestConfigJsonData questConfigJsonData, List<string> detectLoopLog, Dictionary<string, QuestConfigData> alreadyMadeConfigs, Dictionary<string, QuestConfigJsonData> keyQuestIdJsonConfigs)
        {
            
            if (detectLoopLog.Contains(questConfigJsonData.QuestId))
            {
                //TODO 
                Console.WriteLine("[ConfigLoadLog] ModId:" + questConfigJsonData.ModId + "。。　Id:" + questConfigJsonData.QuestId);
                return new List<QuestConfigData>();
            }

            detectLoopLog.Add(questConfigJsonData.QuestId);

            //-----------------------------------------------------
            
            var prerequisiteQuests = new List<QuestConfigData>();
            foreach (var prerequisiteId in questConfigJsonData.Prerequisite)
            {
                
                if (alreadyMadeConfigs.TryGetValue(prerequisiteId, out var prerequisiteQuest))
                {
                    prerequisiteQuests.Add(prerequisiteQuest);
                    continue;
                }

                //Json
                if (!keyQuestIdJsonConfigs.TryGetValue(prerequisiteId, out var prerequisiteJsonConfig))
                {
                    //TODO 
                    Console.WriteLine("[ConfigLoadLog] ModId:" + questConfigJsonData.ModId + " " + questConfigJsonData.QuestId + "ID。　Id:" + prerequisiteId);
                    continue;
                }


                
                var newPrerequisiteQuests = AssemblyPrerequisiteQuests(itemStackFactory, prerequisiteJsonConfig, detectLoopLog, alreadyMadeConfigs, keyQuestIdJsonConfigs);
                
                var newRequestQuest = prerequisiteJsonConfig.ToQuestConfigData(newPrerequisiteQuests, itemStackFactory);
                
                prerequisiteQuests.Add(newRequestQuest);
                alreadyMadeConfigs.Add(newRequestQuest.QuestId, newRequestQuest);
            }

            return prerequisiteQuests;
        }



        ///     JSONQuestConfigJsonData

        /// <param name="questJsons">modIdJson</param>
        /// <returns>ID</returns>
        private static Dictionary<string, QuestConfigJsonData> LoadJsonToQuestConfigJsonData(Dictionary<string, string> questJsons)
        {
            var keyQuestIdConfigs = new Dictionary<string, QuestConfigJsonData>();

            foreach (var questJson in questJsons)
            {
                var modId = questJson.Key;
                
                var loadedQuests = JsonConvert.DeserializeObject<QuestConfigJsonData[]>(questJson.Value);
                if (loadedQuests == null)
                {
                    //TODO 
                    Console.WriteLine("[ConfigLoadLog] ModId:" + modId + "。JSON。");
                    continue;
                }

                
                foreach (var quest in loadedQuests)
                {
                    quest.ModId = modId;
                    keyQuestIdConfigs.Add(quest.QuestId, quest);
                }
            }

            return keyQuestIdConfigs;
        }

        private static Dictionary<string, List<string>> CreateModIdToQuestId(Dictionary<string, QuestConfigData> alreadyMadeConfigs)
        {
            Dictionary<string, List<string>> modIdToQuests = new();

            foreach (var quest in alreadyMadeConfigs.Values)
            {
                if (modIdToQuests.TryGetValue(quest.ModId, out var questIdList))
                {
                    questIdList.Add(quest.QuestId);
                    continue;
                }

                modIdToQuests.Add(quest.ModId, new List<string> { quest.QuestId });
            }

            return modIdToQuests;
        }
    }
}