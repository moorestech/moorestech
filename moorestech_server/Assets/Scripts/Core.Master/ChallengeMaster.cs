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
        
        public ChallengeCategoryMasterElement GetChallengeCategory(Guid guid)
        {
            throw new NotImplementedException();
        }

    }
}