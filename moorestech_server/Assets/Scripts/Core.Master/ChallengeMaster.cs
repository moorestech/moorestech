using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master.Validator;
using Mooresmaster.Loader.ChallengesModule;
using Mooresmaster.Model.ChallengesModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class ChallengeMaster : IMasterValidator
    {
        public readonly Challenges Challenges;
        public ChallengeCategoryMasterElement[] ChallengeCategoryMasterElements => Challenges.Data;

        private Dictionary<Guid, ChallengeCategoryMasterElement> _challengeCategoryGuidMap;
        private Dictionary<Guid, ChallengeMasterElement> _challengeGuidMap;
        private Dictionary<Guid, ChallengeCategoryMasterElement> _challengeToCategoryMap;
        private Dictionary<Guid, List<Guid>> _nextChallenges;

        public ChallengeMaster(JToken challengeJToken)
        {
            Challenges = ChallengesLoader.Load(challengeJToken);
        }

        public bool Validate(out string errorLogs)
        {
            return ChallengeMasterUtil.Validate(Challenges, out errorLogs);
        }

        public void Initialize()
        {
            ChallengeMasterUtil.Initialize(Challenges, out _challengeCategoryGuidMap, out _challengeGuidMap, out _challengeToCategoryMap, out _nextChallenges);
        }
        
        public List<ChallengeMasterElement> GetNextChallenges(Guid challengeGuid)
        {
            if (!_nextChallenges.TryGetValue(challengeGuid, out var nextChallenges))
            {
                throw new InvalidOperationException($"Next challenges not found. ChallengeGuid:{challengeGuid}");
            }
            
            return nextChallenges.ConvertAll(GetChallenge);
        }
        
        public ChallengeMasterElement GetChallenge(Guid guid)
        {
            return _challengeGuidMap[guid];
        }
        
        public ChallengeCategoryMasterElement GetChallengeCategoryFromChallengeGuid(Guid guid)
        {
            return _challengeToCategoryMap[guid];
        }
        
        /// <summary>
        /// 指定されたカテゴリの初期チャレンジ（前提条件がないチャレンジ）を取得する
        /// </summary>
        public List<ChallengeMasterElement> GetCategoryInitialChallenges(Guid categoryGuid)
        {
            var category = ChallengeCategoryMasterElements.FirstOrDefault(c => c.CategoryGuid == categoryGuid);
            if (category == null) return new List<ChallengeMasterElement>();
            
            var initialChallenges = new List<ChallengeMasterElement>();
            foreach (var challengeElement in category.Challenges)
            {
                // 前提条件がないチャレンジを初期チャレンジとする
                if (challengeElement.PrevChallengeGuids == null || challengeElement.PrevChallengeGuids.Length == 0)
                {
                    initialChallenges.Add(challengeElement);
                }
            }
            
            return initialChallenges;
        }
        
        public ChallengeCategoryMasterElement GetChallengeCategory(Guid categoryGuid)
        {
            return _challengeCategoryGuidMap[categoryGuid];
        }
    }
}