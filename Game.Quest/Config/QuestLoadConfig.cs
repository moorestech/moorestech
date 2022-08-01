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
    /// <summary>
    /// クエストコンフィグからのロードを行う
    /// 前提クエストのリスト構築を行う
    /// </summary>
    internal static class QuestLoadConfig
    {
        public static (Dictionary<string, List<string>> ModIdToQuests, Dictionary<string, QuestConfigData> QuestIdToQuestConfigs) LoadConfig(ItemStackFactory itemStackFactory,Dictionary<string,string> blockJsons)
        {
            var questIdConfig = CreateQuestIdConfig(itemStackFactory,blockJsons);
            var modIdToQuests = CreateModIdToQuestId(questIdConfig);

            return (modIdToQuests, questIdConfig);
        }

        private static Dictionary<string, QuestConfigData> CreateQuestIdConfig(ItemStackFactory itemStackFactory,Dictionary<string,string> blockJsons)
        {
            Dictionary<string, QuestConfigData> alreadyMadeConfigs = new();
            var jsonQuestConfig = LoadJsonToQuestConfigJsonData(blockJsons);
            
            //JSONからロードした生データをクエストコンフィグに変換する
            foreach (var jsonConfig in jsonQuestConfig.Values)
            {
                if (alreadyMadeConfigs.ContainsKey(jsonConfig.QuestId)) continue;
                
                //前提クエストを探索、作成
                var prerequisiteQuests = AssemblyPrerequisiteQuests(itemStackFactory,jsonConfig, new List<string>(), alreadyMadeConfigs, jsonQuestConfig);
                
                //探索した結果前提クエストのなかに組み込まれていた場合はスルーする（おそらく前提クエストでループが発生した時これがtrueになる）
                if (alreadyMadeConfigs.ContainsKey(jsonConfig.QuestId)) continue;
                alreadyMadeConfigs.Add(jsonConfig.QuestId,jsonConfig.ToQuestConfigData(prerequisiteQuests,itemStackFactory));
            }

            return alreadyMadeConfigs;
        }   


        /// <summary>
        /// 再帰を使って前提クエストの探索、作成をする
        /// </summary>
        /// <param name="itemStackFactory">リワードアイテムを作る用のitemStackFactory</param>
        /// <param name="questConfigJsonData">前提クエストのリストを作りたいjsonコンフィグ</param>
        /// <param name="detectLoopLog">ループ検知用のログ。再帰以外の呼び出し時は新しく作る。</param>
        /// <param name="alreadyMadeConfigs">事前に作られたクエストの流用のために、すでに作られたクエストの辞書を渡す。再帰中に新しく作った場合は</param>
        /// <param name="keyQuestIdJsonConfigs"></param>
        /// <returns></returns>
        private static List<QuestConfigData> AssemblyPrerequisiteQuests(ItemStackFactory itemStackFactory,QuestConfigJsonData questConfigJsonData,List<string> detectLoopLog,Dictionary<string,QuestConfigData> alreadyMadeConfigs,Dictionary<string, QuestConfigJsonData> keyQuestIdJsonConfigs)
        {
            //ループがないかチェックする
            if (detectLoopLog.Contains(questConfigJsonData.QuestId))
            {
                //TODO 例外を出力す方法を考える
                Console.WriteLine("[ConfigLoadLog] ModId:" + questConfigJsonData.ModId + "の前提クエストにループがありました。前提クエストをチェックしてください。　クエストId:"+questConfigJsonData.QuestId);
                return new List<QuestConfigData>();
            }
            detectLoopLog.Add(questConfigJsonData.QuestId);
            
            //ここから実際の前提クエスト構築処理-----------------------------------------------------
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
                    Console.WriteLine("[ConfigLoadLog] ModId:" + questConfigJsonData.ModId + "のクエスト "+questConfigJsonData.QuestId  +"の前提クエストに存在しないクエストIDが渡されました。　存在しないクエストId:"+prerequisiteId);
                    continue;
                }
                
                
                
                //再帰を使って前提クエストの前提クエストを取得
                var newPrerequisiteQuests = AssemblyPrerequisiteQuests(itemStackFactory,prerequisiteJsonConfig, detectLoopLog, alreadyMadeConfigs, keyQuestIdJsonConfigs);
                //前提クエストを作成
                var newRequestQuest = prerequisiteJsonConfig.ToQuestConfigData(newPrerequisiteQuests, itemStackFactory);
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
        private static Dictionary<string, QuestConfigJsonData> LoadJsonToQuestConfigJsonData(Dictionary<string,string> questJsons)
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
                    keyQuestIdConfigs.Add(quest.QuestId,quest);
                }
            }

            return keyQuestIdConfigs;
        }

        private static Dictionary<string, List<string>> CreateModIdToQuestId(Dictionary<string, QuestConfigData> alreadyMadeConfigs)
        {
            Dictionary<string, List<string>> modIdToQuests = new();

            foreach (var quest in alreadyMadeConfigs.Values)
            {
                if (modIdToQuests.TryGetValue(quest.ModId,out var questIdList))
                {
                 
                    questIdList.Add(quest.QuestId);
                    continue;
                }
                modIdToQuests.Add(quest.ModId,new List<string>{quest.QuestId});
            }

            return modIdToQuests;
        }
    }
}