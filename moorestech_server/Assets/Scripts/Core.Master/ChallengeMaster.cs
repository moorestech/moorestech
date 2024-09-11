using System;
using System.Collections.Generic;
using Mooresmaster.Loader.ChallengesModule;
using Mooresmaster.Model.ChallengesModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class ChallengeMaster
    {
        public Challenges Challenges;
        public readonly List<Guid> InitialChallenge;
        
        private readonly Dictionary<Guid, List<Guid>> _nextChallenges;
        
        public ChallengeMaster(JToken challengeJToken)
        {
            Challenges = ChallengesLoader.Load(challengeJToken);
            _nextChallenges = new Dictionary<Guid, List<Guid>>();
            foreach (var challengeElement in Challenges.Data)
            {
                var next = new List<Guid>();
                foreach (var checkTarget in Challenges.Data)
                {
                    if (challengeElement.ChallengeGuid == checkTarget.PrevChallengeGuid)
                    {
                        next.Add(checkTarget.ChallengeGuid);
                    }
                }
                
                _nextChallenges.Add(challengeElement.ChallengeGuid, next);
            }
            
            InitialChallenge = new List<Guid>();
            foreach (var challengeElement in Challenges.Data)
            {
                // prevが自分自身のものが初期チャレンジとして扱う
                if (challengeElement.ChallengeGuid == challengeElement.PrevChallengeGuid)
                {
                    InitialChallenge.Add(challengeElement.ChallengeGuid);
                }
            }
        }
        
        public List<Guid> GetNextChallenges(Guid challengeGuid)
        {
            if (!_nextChallenges.TryGetValue(challengeGuid, out var nextChallenges))
            {
                throw new InvalidOperationException($"Next challenges not found. ChallengeGuid:{challengeGuid}");
            }
            return nextChallenges;
        }
        
        public ChallengeElement GetChallenge(Guid guid)
        {
            return Array.Find(Challenges.Data, x => x.ChallengeGuid == guid);
        }
    }
}