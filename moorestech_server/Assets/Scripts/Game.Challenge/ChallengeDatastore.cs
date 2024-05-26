using System;
using System.Collections.Generic;
using Core.Update;
using Game.Challenge.Task;
using UniRx;

namespace Game.Challenge
{
    public class ChallengeDatastore
    {
        private readonly Dictionary<int, PlayerChallengeInfo> _playerChallengeInfos = new();

        private readonly ChallengeConfig _challengeConfig;

        public ChallengeDatastore(ChallengeConfig challengeConfig, Dictionary<int, List<int>> loadedCompletedChallengeIds)
        {
            GameUpdater.UpdateObservable.Subscribe(Update);
            _challengeConfig = challengeConfig;

            foreach (var loadedChallenge in loadedCompletedChallengeIds)
            {
                var playerId = loadedChallenge.Key;
                var currentChallenges = new List<CurrentChallenge>();

                foreach (var completedChallengeId in loadedCompletedChallengeIds[playerId])
                {
                    // 完了したチャレンジの次のチャレンジがクリア済みでなければ、CurrentChallengeに追加
                    var info = challengeConfig.GetChallenge(completedChallengeId);
                    foreach (var nextId in info.NextIds)
                    {
                        if (loadedCompletedChallengeIds[playerId].Contains(nextId)) continue;

                        var currentChallenge = new CurrentChallenge(playerId, challengeConfig.ChallengeInfos[nextId]);
                        currentChallenge.OnChallengeComplete.Subscribe(CompletedChallenge);
                        currentChallenges.Add(currentChallenge);
                    }
                }

                _playerChallengeInfos.Add(playerId, new PlayerChallengeInfo(currentChallenges, loadedChallenge.Value));
            }
        }

        private void CompletedChallenge(CurrentChallenge currentChallenge)
        {
            var playerId = currentChallenge.PlayerId;
            var challengeInfo = _playerChallengeInfos[playerId];

            challengeInfo.CurrentChallenges.Remove(currentChallenge);
            challengeInfo.CompletedChallengeIds.Add(currentChallenge.Config.Id);

            var nextIds = currentChallenge.Config.NextIds;
            foreach (var nextId in nextIds)
            {
                var config = _challengeConfig.GetChallenge(nextId);

                var nextChallenge = new CurrentChallenge(playerId, config);
                nextChallenge.OnChallengeComplete.Subscribe(CompletedChallenge);
                challengeInfo.CurrentChallenges.Remove(currentChallenge);
            }
        }

        private void Update(Unit unit)
        {
            foreach (var challengeInfo in _playerChallengeInfos.Values)
            {
                foreach (var currentChallenge in challengeInfo.CurrentChallenges)
                {
                    currentChallenge.ManualUpdate();
                }
            }
        }

        public PlayerChallengeInfo GetChallengeInfo(int playerId)
        {
            if (_playerChallengeInfos.TryGetValue(playerId, out var info))
            {
                return info;
            }

            var initialChallenge = CreateInitialChallenge();
            _playerChallengeInfos.Add(playerId, initialChallenge);

            return initialChallenge;

            #region Internal

            PlayerChallengeInfo CreateInitialChallenge()
            {
                var initialChallenges = new List<CurrentChallenge>();
                foreach (var initialChallengeConfig in _challengeConfig.InitialChallenges)
                {
                    initialChallenges.Add(new CurrentChallenge(playerId, initialChallengeConfig));
                }

                return new PlayerChallengeInfo(initialChallenges, new List<int>());
            }

            #endregion
        }
    }

    public class PlayerChallengeInfo
    {
        public List<CurrentChallenge> CurrentChallenges { get; }
        public List<int> CompletedChallengeIds { get; }

        public PlayerChallengeInfo(List<CurrentChallenge> currentChallenges, List<int> completedChallengeIds)
        {
            CurrentChallenges = currentChallenges;
            CompletedChallengeIds = completedChallengeIds;
        }
    }
}