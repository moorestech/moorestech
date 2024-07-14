using System.Collections.Generic;
using System.Linq;
using Core.ConfigJson;
using Game.Challenge.Config.TutorialParam;
using Newtonsoft.Json.Linq;

namespace Game.Challenge
{
    public class ChallengeConfig : IChallengeConfig
    {
        public IReadOnlyList<ChallengeInfo> InitialChallenges { get; }
        
        private readonly Dictionary<int, ChallengeInfo> _challengeInfos = new();
        
        public ChallengeConfig(ConfigJsonFileContainer configJson)
        {
            // パラメーターのローダーを定義
            // define parameter loader
            var challengeTaskParamLoader = new Dictionary<string, ChallengeTaskParamLoader>();
            challengeTaskParamLoader.Add(CreateItemTaskParam.TaskCompletionType, CreateItemTaskParam.Create);
            challengeTaskParamLoader.Add(InInventoryItemTaskParam.TaskCompletionType, InInventoryItemTaskParam.Create);
            
            var tutorialTaskParamLoader = new Dictionary<string, TutorialParamLoader>();
            tutorialTaskParamLoader.Add(MapObjectPinTutorialParam.TaskCompletionType, MapObjectPinTutorialParam.Create);
            tutorialTaskParamLoader.Add(KeyControlTutorialParam.TaskCompletionType, KeyControlTutorialParam.Create);
            tutorialTaskParamLoader.Add(UIHighLightTutorialParam.TaskCompletionType, UIHighLightTutorialParam.Create);
            
            // 双方向ID構築のため、一時的なチャレンジ情報をロード
            // load temporary challenge information for bidirectional ID construction
            var tmpChallenges = new Dictionary<int, TmpChallengeInfo>();
            foreach (var jsonText in configJson.SortedChallengeConfigJsonList) LoadTmpChallengeInfo(jsonText);
            var nextChallengeIds = new Dictionary<int, List<int>>();
            foreach (var tmpChallenge in tmpChallenges.Values)
            {
                if (!nextChallengeIds.ContainsKey(tmpChallenge.PreviousId)) nextChallengeIds.Add(tmpChallenge.PreviousId, new List<int>());
                nextChallengeIds[tmpChallenge.PreviousId].Add(tmpChallenge.Id);
            }
            
            // チャレンジ情報を生成
            // generate challenge information
            foreach (var tmpChallenge in tmpChallenges.Values)
            {
                var nextIds = nextChallengeIds.TryGetValue(tmpChallenge.Id, out var ids) ? ids : new List<int>();
                _challengeInfos.Add(tmpChallenge.Id, new ChallengeInfo(tmpChallenge, nextIds));
            }
            
            InitialChallenges = _challengeInfos.Values.Where(challenge => challenge.PreviousId == -1).ToList();
            
            #region Intenral
            
            void LoadTmpChallengeInfo(string jsonText)
            {
                dynamic challengeJson = JObject.Parse(jsonText);
                
                foreach (var challenge in challengeJson.challenges)
                {
                    int id = challenge.id;
                    string taskCompletionType = challenge.taskCompletionType;
                    var taskParamLoader = challengeTaskParamLoader[taskCompletionType];
                    IChallengeTaskParam taskParam = taskParamLoader.Invoke(challenge.taskParam);
                    
                    int previousId = challenge.prevId;
                    string summary = challenge.summary;
                    string fireSkitType = challenge.fireSkitType;
                    string fireSkitName = challenge.fireSkitName;
                    
                    var tutorials = new List<TutorialConfig>();
                    if (challenge.tutorials != null)
                        foreach (var tutorial in challenge.tutorials)
                        {
                            string tutorialType = tutorial.tutorialType;
                            ITutorialParam tutorialTaskParam = tutorialTaskParamLoader[tutorialType].Invoke(tutorial.tutorialParam);
                            
                            tutorials.Add(new TutorialConfig(tutorialType, tutorialTaskParam));
                        }
                    
                    tmpChallenges.Add(id, new TmpChallengeInfo
                    {
                        Id = id,
                        PreviousId = previousId,
                        TaskCompletionType = taskCompletionType,
                        TaskParam = taskParam,
                        Summary = summary,
                        FireSkitType = fireSkitType,
                        FireSkitName = fireSkitName,
                        Tutorials = tutorials
                    });
                }
            }
            
            #endregion
        }
        
        public ChallengeInfo GetChallenge(int playerId)
        {
            return _challengeInfos[playerId];
        }
    }
}