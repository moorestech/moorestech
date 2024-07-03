using System.Collections.Generic;
using System.Linq;
using Core.ConfigJson;
using Newtonsoft.Json.Linq;

namespace Game.Challenge
{
    public class ChallengeConfig
    {
        private readonly Dictionary<int, ChallengeInfo> _challengeInfos = new();
        
        public readonly IReadOnlyList<ChallengeInfo> InitialChallenges;
        
        public ChallengeConfig(ConfigJsonFileContainer configJson)
        {
            var challengeTaskParamLoader = new Dictionary<string, ChallengeTaskParamLoader>();
            challengeTaskParamLoader.Add(CreateItemTaskParam.TaskCompletionType, CreateItemTaskParam.Create);
            challengeTaskParamLoader.Add(InInventoryItemTaskParam.TaskCompletionType, InInventoryItemTaskParam.Create);
            
            var tmpChallenges = new Dictionary<int, TmpChallengeInfo>();
            foreach (var jsonText in configJson.SortedChallengeConfigJsonList) LoadTmpChallengeInfo(jsonText);
            var nextChallengeIds = new Dictionary<int, List<int>>();
            foreach (var tmpChallenge in tmpChallenges.Values)
            {
                if (!nextChallengeIds.ContainsKey(tmpChallenge.PreviousId)) nextChallengeIds.Add(tmpChallenge.PreviousId, new List<int>());
                nextChallengeIds[tmpChallenge.PreviousId].Add(tmpChallenge.Id);
            }
            
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
                    
                    tmpChallenges.Add(id, new TmpChallengeInfo
                    {
                        Id = id,
                        PreviousId = previousId,
                        TaskCompletionType = taskCompletionType,
                        TaskParam = taskParam,
                        Summary = summary,
                        FireSkitType = fireSkitType,
                        FireSkitName = fireSkitName,
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
    
    public class TmpChallengeInfo
    {
        public int Id;
        public int PreviousId;
        
        public string Summary;
        
        public string TaskCompletionType;
        public IChallengeTaskParam TaskParam;
        
        public string FireSkitType;
        public string FireSkitName;
        
    }
}