using System.Collections.Generic;
using Game.Challenge.Task;

namespace Game.Challenge
{
    public class ChallengeDatastore
    {
        private readonly Dictionary<int, List<CurrentChallenge>> _currentChallenges = new(); // Key: PlayerId Value: Current Challenge
        private readonly Dictionary<int,List<int>> _completedChallengeIds = new(); // Key: PlayerId Value: Completed Challenge Ids

        public void Update()
        {
            // check
            
            // notify client
            
        }
    }
}