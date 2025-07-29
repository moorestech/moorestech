using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Loader.ChallengesModule;
using Mooresmaster.Model.ChallengesModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class ChallengeMaster
    {
        public readonly Challenges Challenges;
        public ChallengeCategoryMasterElement[] ChallengeCategoryMasterElements => Challenges.Data;
        
        private readonly Dictionary<Guid, ChallengeMasterElement> _challengeGuidMap = new();
        private readonly Dictionary<Guid, ChallengeCategoryMasterElement> _challengeToCategoryMap = new();
        private readonly Dictionary<Guid, List<Guid>> _nextChallenges;
        
        public ChallengeMaster(JToken challengeJToken)
        {
            Challenges = ChallengesLoader.Load(challengeJToken);
            _nextChallenges = new Dictionary<Guid, List<Guid>>();
            foreach (var challengeCategory in Challenges.Data)
            {
                foreach (var challengeElement in challengeCategory.Challenges)
                {
                    var next = new List<Guid>();
                    foreach (var checkTarget in challengeCategory.Challenges)
                    {
                        var prev = checkTarget.PrevChallengeGuids;
                        if (prev != null && prev.Contains(challengeElement.ChallengeGuid))
                        {
                            next.Add(checkTarget.ChallengeGuid);
                        }
                    }
                    
                    _nextChallenges.Add(challengeElement.ChallengeGuid, next);
                    _challengeGuidMap.Add(challengeElement.ChallengeGuid, challengeElement);
                    _challengeToCategoryMap.Add(challengeElement.ChallengeGuid, challengeCategory);
                }
            }
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
    }
}