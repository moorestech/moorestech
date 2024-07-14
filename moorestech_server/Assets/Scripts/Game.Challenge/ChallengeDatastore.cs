using System.Collections.Generic;
using Core.Update;
using Game.Challenge.Task;
using Game.Challenge.Task.Factory;
using Game.Context;
using UniRx;

namespace Game.Challenge
{
    public class ChallengeDatastore
    {
        private readonly Dictionary<int, PlayerChallengeInfo> _playerChallengeInfos = new();
        private readonly ChallengeFactory _challengeFactory = new();
        
        public ChallengeDatastore()
        {
            GameUpdater.UpdateObservable.Subscribe(Update);
        }
        
        private void Update(Unit unit)
        {
            foreach (var challengeInfo in _playerChallengeInfos.Values)
                for (var i = challengeInfo.CurrentChallenges.Count - 1; i >= 0; i--)
                {
                    var currentChallenge = challengeInfo.CurrentChallenges[i];
                    currentChallenge.ManualUpdate();
                }
        }
        
        public PlayerChallengeInfo GetOrCreateChallengeInfo(int playerId)
        {
            if (_playerChallengeInfos.TryGetValue(playerId, out var info)) return info;
            
            var initialChallenge = CreateInitialChallenge();
            _playerChallengeInfos.Add(playerId, initialChallenge);
            
            return initialChallenge;
            
            #region Internal
            
            PlayerChallengeInfo CreateInitialChallenge()
            {
                var initialChallenges = new List<IChallengeTask>();
                foreach (var initialChallengeConfig in ServerContext.ChallengeConfig.InitialChallenges)
                {
                    var initialChallenge = CreateChallenge(playerId, initialChallengeConfig);
                    initialChallenges.Add(initialChallenge);
                }
                
                return new PlayerChallengeInfo(initialChallenges, new List<int>());
            }
            
            #endregion
        }
        
        public void LoadChallenge(List<ChallengeJsonObject> challengeJsonObjects)
        {
            foreach (var challengeJsonObject in challengeJsonObjects)
            {
                var playerId = challengeJsonObject.PlayerId;
                var currentChallenges = new List<IChallengeTask>();
                
                // InitialChallengeの中でクリアしていないのを登録
                foreach (var initialChallengeConfig in ServerContext.ChallengeConfig.InitialChallenges)
                {
                    // クリア済みならスキップ
                    if (challengeJsonObject.CompletedIds.Contains(initialChallengeConfig.Id)) continue;
                    
                    var initialChallenge = CreateChallenge(playerId, initialChallengeConfig);
                    currentChallenges.Add(initialChallenge);
                }
                
                // CurrentChallengeを作成
                foreach (var completedId in challengeJsonObject.CompletedIds)
                {
                    // 完了したチャレンジの次のチャレンジがクリア済みでなければ、CurrentChallengeに追加
                    var info = ServerContext.ChallengeConfig.GetChallenge(completedId);
                    
                    foreach (var nextId in info.NextIds)
                    {
                        if (challengeJsonObject.CompletedIds.Contains(nextId)) continue;
                        
                        var nextInfo = ServerContext.ChallengeConfig.GetChallenge(nextId);
                        var initialChallenge = CreateChallenge(playerId, nextInfo);
                        currentChallenges.Add(initialChallenge);
                    }
                }
                
                _playerChallengeInfos.Add(playerId, new PlayerChallengeInfo(currentChallenges, challengeJsonObject.CompletedIds));
            }
        }
        
        private IChallengeTask CreateChallenge(int playerId, ChallengeInfo config)
        {
            var challenge = _challengeFactory.CreateChallengeTask(playerId, config);
            challenge.OnChallengeComplete.Subscribe(CompletedChallenge);
            return challenge;
        }
        
        private void CompletedChallenge(IChallengeTask currentChallenge)
        {
            var playerId = currentChallenge.PlayerId;
            var challengeInfo = _playerChallengeInfos[playerId];
            
            challengeInfo.CurrentChallenges.Remove(currentChallenge);
            challengeInfo.CompletedChallengeIds.Add(currentChallenge.Config.Id);
            
            var nextIds = currentChallenge.Config.NextIds;
            foreach (var nextId in nextIds)
            {
                var config = ServerContext.ChallengeConfig.GetChallenge(nextId);
                
                var nextChallenge = CreateChallenge(playerId, config);
                challengeInfo.CurrentChallenges.Add(nextChallenge);
            }
            
            ServerContext.GetService<ChallengeEvent>().InvokeCompleteChallenge(currentChallenge);
        }

        
        public List<ChallengeJsonObject> GetSaveJsonObject()
        {
            var result = new List<ChallengeJsonObject>();
            foreach (var challengeInfo in _playerChallengeInfos)
            {
                var playerId = challengeInfo.Key;
                var completedIds = challengeInfo.Value.CompletedChallengeIds;
                
                result.Add(new ChallengeJsonObject
                {
                    PlayerId = playerId,
                    CompletedIds = completedIds
                });
            }
            
            return result;
        }
    }
    
    public class PlayerChallengeInfo
    {
        public List<IChallengeTask> CurrentChallenges { get; }
        public List<int> CompletedChallengeIds { get; }
        
        public PlayerChallengeInfo(List<IChallengeTask> currentChallenges, List<int> completedChallengeIds)
        {
            CurrentChallenges = currentChallenges;
            CompletedChallengeIds = completedChallengeIds;
        }
    }
}