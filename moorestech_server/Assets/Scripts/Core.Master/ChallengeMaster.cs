using System;
using System.Collections.Generic;

namespace Core.Master
{
    public class ChallengeMaster
    {
        private static Dictionary<Guid, List<Guid>> _nextChallenges;
        
        public static List<Guid> GetNextChallenges(Guid challengeGuid)
        {
            _nextChallenges ??= BuildNextChallenges();
            
            if (!_nextChallenges.TryGetValue(challengeGuid, out var nextChallenges))
            {
                throw new InvalidOperationException($"Next challenges not found. ChallengeGuid:{challengeGuid}");
            }
            return nextChallenges;
        }
        
        private static Dictionary<Guid, List<Guid>> BuildNextChallenges()
        {
            var nextChallenges = new Dictionary<Guid, List<Guid>>();
            foreach (var challengeElement in MasterHolder.Challenges.Data)
            {
                var next = new List<Guid>();
                foreach (var checkTarget in MasterHolder.Challenges.Data)
                {
                    if (challengeElement.ChallengeGuid == checkTarget.PrevChallengeGuid)
                    {
                        next.Add(checkTarget.ChallengeGuid);
                    }
                }
                
                nextChallenges.Add(challengeElement.ChallengeGuid, next);
            }
            
            return nextChallenges;
        }
    }
}