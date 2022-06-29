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
        private readonly ItemStackFactory _itemStackFactory;

        public QuestLoadConfig(ItemStackFactory itemStackFactory)
        {
            _itemStackFactory = itemStackFactory;
        }


        public (Dictionary<string, List<string>> ModIdToQuest, Dictionary<string, List<QuestConfigData>> QuestConfigs) LoadConfig(Dictionary<string,string> blockJsons)
        {
            var jsonQuestConfig = LoadJsonToQuestConfigJsonData(blockJsons);
            
            var
        }

        
        
        /// <summary>
        /// 再帰を使って前提クエストの探索、作成をする
        /// </summary>
        /// <param name="questConfigJsonData">前提クエストのリストを作りたいjsonコンフィグ</param>
        /// <param name="detectLoopLog">ループ検知用のログ。再帰以外の呼び出し時は新しく作る。</param>
        /// <param name="alreadyMadeConfigs">事前に作られたクエストの流用のために、すでに作られたクエストの辞書を渡す。再帰中に新しく作った場合は</param>
        /// <param name="keyQuestIdJsonConfigs"></param>
        /// <returns></returns>
        private List<QuestConfigData> AssemblyPrerequisiteQuests(QuestConfigJsonData questConfigJsonData,List<string> detectLoopLog,Dictionary<string,QuestConfigData> alreadyMadeConfigs,Dictionary<string, QuestConfigJsonData> keyQuestIdJsonConfigs)
        {
            //ループがないかチェックする
            if (detectLoopLog.Contains(questConfigJsonData.Id))
            {
                //TODO 例外を出力す方法を考える
                Console.WriteLine("[ConfigLoadLog] ModId:" + questConfigJsonData.ModId + "の前提クエストにループがありました。前提クエストをチェックしてください。　クエストId:"+questConfigJsonData.Id);
                return new List<QuestConfigData>();
            }
            
            detectLoopLog.Add(questConfigJsonData.Id);
            
            //前提クエストを探索、作成するループ
            var prerequisiteQuests = new List<QuestConfigData>();
            foreach (var prerequisiteId in questConfigJsonData.Prerequisite)
            {
                //すでに作ったコンフィグがあるかチェック
                if (alreadyMadeConfigs.TryGetValue(prerequisiteId,out var prerequisiteQuest))
                {
                    prerequisiteQuests.Add(prerequisiteQuest);
                    continue;
                }
                
                //なかったのでJsonコンフィグにあるかチェック
                if (!keyQuestIdJsonConfigs.TryGetValue(prerequisiteId,out var prerequisiteJsonConfig))
                {
                    //TODO 例外を出力す方法を考える
                    Console.WriteLine("[ConfigLoadLog] ModId:" + questConfigJsonData.ModId + "のクエスト "+questConfigJsonData.Id  +"前提クエストに存在しないクエストIDが渡されました。　存在しないクエストId:"+prerequisiteId);
                    continue;
                }
                
                
                
                //再帰を使って前提クエストの前提クエストを取得
                var newPrerequisiteQuests = AssemblyPrerequisiteQuests(prerequisiteJsonConfig, detectLoopLog, alreadyMadeConfigs, keyQuestIdJsonConfigs);
                //前提クエストを作成
                var newRequestQuest = prerequisiteJsonConfig.ToQuestConfigData(newPrerequisiteQuests, _itemStackFactory);
                //前提クエストリストと辞書に登録
                prerequisiteQuests.Add(newRequestQuest);
                alreadyMadeConfigs.Add(newRequestQuest.QuestId,newRequestQuest);
            }

            return prerequisiteQuests;
        }
        
        
        
        /// <summary>
        /// JSONからQuestConfigJsonDataをロードし、
        /// </summary>
        /// <param name="questJsons">modIdとJsonの辞書</param>
        /// <returns>クエストIDがキーの辞書</returns>
        private Dictionary<string, QuestConfigJsonData> LoadJsonToQuestConfigJsonData(Dictionary<string,string> questJsons)
        {
            var keyQuestIdConfigs = new Dictionary<string, QuestConfigJsonData>();
            
            foreach (var questJson in questJsons)
            {
                var modId = questJson.Key;
                //クエストのロード
                var loadedQuests = JsonConvert.DeserializeObject<QuestConfigJsonData[]>(questJson.Value);
                if (loadedQuests == null)
                {
                    //TODO 例外を出力す方法を考える
                    Console.WriteLine("[ConfigLoadLog] ModId:" + modId + "のクエストコンフィグのロードに失敗しました。JSONファイルを確認してください。");
                    continue;
                }

                //辞書に格納
                foreach (var quest in loadedQuests)
                {
                    quest.ModId = modId;
                    keyQuestIdConfigs.Add(quest.Id,quest);
                }
            }

            return keyQuestIdConfigs;
        }
    }
    

    [JsonObject("SpaceAssets")]
    internal class QuestConfigJsonData
    {
        [JsonIgnore]
        public string ModId = null;
        
        
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
        public static QuestConfigData ToQuestConfigData(this QuestConfigJsonData questConfigJsonData,List<QuestConfigData> prerequisiteQuests,ItemStackFactory itemStackFactory)
        {
            var rewardItems = new List<IItemStack>();
            for (int i = 0; i < questConfigJsonData.RewardItem.GetLength(0); i++)
            {
                var id = questConfigJsonData.RewardItem[i, 0];
                var count = questConfigJsonData.RewardItem[i, 1];
                rewardItems.Add(itemStackFactory.Create(id,count));
            }
            
            
            return new QuestConfigData(
                questConfigJsonData.ModId,
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