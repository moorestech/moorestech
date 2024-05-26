using System.Collections.Generic;
using Core.Update;
using Game.Challenge.Task;
using UniRx;

namespace Game.Challenge
{
    public class ChallengeDatastore
    {
        private readonly Dictionary<int, List<CurrentChallenge>> _currentChallenges = new(); // Key: PlayerId Value: Current Challenge
        private readonly Dictionary<int, List<int>> _completedChallengeIds = new(); // Key: PlayerId Value: Completed Challenge Ids

        private readonly ChallengeConfig _challengeConfig;

        public ChallengeDatastore(ChallengeConfig challengeConfig, Dictionary<int, List<int>> loadedCompletedChallengeIds)
        {
            GameUpdater.UpdateObservable.Subscribe(Update);
            _challengeConfig = challengeConfig;
            _completedChallengeIds = loadedCompletedChallengeIds;

            foreach (var playerId in _completedChallengeIds.Keys)
            {
                _currentChallenges.Add(playerId, new List<CurrentChallenge>());

                foreach (var completedChallengeId in _completedChallengeIds[playerId])
                {
                    // 完了したチャレンジの次のチャレンジがクリア済みでなければ、CurrentChallengeに追加
                    var info = challengeConfig.GetChallenge(completedChallengeId);
                    foreach (var nextId in info.NextIds)
                    {
                        if (_completedChallengeIds[playerId].Contains(nextId)) continue;

                        var currentChallenge = new CurrentChallenge(playerId, challengeConfig.ChallengeInfos[nextId]);
                        currentChallenge.OnChallengeComplete.Subscribe(CompletedChallenge);
                        _currentChallenges[playerId].Add(currentChallenge);
                    }
                }
            }
        }

        private void CompletedChallenge(CurrentChallenge currentChallenge)
        {
            var playerId = currentChallenge.PlayerId;
            _currentChallenges[playerId].Remove(currentChallenge);
            _completedChallengeIds[playerId].Add(currentChallenge.Config.Id);

            var nextIds = currentChallenge.Config.NextIds;
            foreach (var nextId in nextIds)
            {
                var config = _challengeConfig.GetChallenge(nextId);

                var nextChallenge = new CurrentChallenge(playerId, config);
                nextChallenge.OnChallengeComplete.Subscribe(CompletedChallenge);
                _currentChallenges[playerId].Add(nextChallenge);
            }
        }

        private void Update(Unit unit)
        {
            foreach (var challenges in _currentChallenges)
            {
                foreach (var challenge in challenges.Value)
                {
                    challenge.ManualUpdate();
                }
            }
        }
        
        public List<int> GetCompletedChallengeIds(int playerId)
        {
            return _completedChallengeIds[playerId];
        }
        
        public List<CurrentChallenge> GetCurrentChallenges(int playerId)
        {
            return _currentChallenges[playerId];
        }
    }
}