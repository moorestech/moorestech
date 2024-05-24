using System.Collections.Generic;
using Game.Challenge.Task;
using UniRx;

namespace Game.Challenge
{
    public class ChallengeDatastore
    {
        private readonly Dictionary<int, List<CurrentChallenge>> _currentChallenges = new(); // Key: PlayerId Value: Current Challenge
        private readonly Dictionary<int, List<int>> _completedChallengeIds = new(); // Key: PlayerId Value: Completed Challenge Ids

        public ChallengeDatastore(ChallengeConfig challengeConfig, Dictionary<int, List<int>> loadedCompletedChallengeIds)
        {
            _completedChallengeIds = loadedCompletedChallengeIds;

            foreach (var playerId in _completedChallengeIds.Keys)
            {
                _currentChallenges.Add(playerId, new List<CurrentChallenge>());

                foreach (var completedChallengeId in _completedChallengeIds[playerId])
                {
                    // 完了したチャレンジの次のチャレンジがクリア済みでなければ、CurrentChallengeに追加
                    var info = challengeConfig.ChallengeInfos[completedChallengeId];
                    foreach (var nextId in info.NextIds)
                    {
                        if (!_completedChallengeIds[playerId].Contains(nextId))
                        {
                            var currentChallenge = new CurrentChallenge(challengeConfig.ChallengeInfos[nextId]);
                            currentChallenge.OnChallengeComplete.Subscribe(CompletedChallenge);
                            _currentChallenges[playerId].Add(currentChallenge);
                        }
                    }
                }
            }
        }

        private void CompletedChallenge(ChallengeInfo challengeInfo)
        {
        }
    }
}