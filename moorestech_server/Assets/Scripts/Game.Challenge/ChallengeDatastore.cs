using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Challenge.Task;
using Game.Challenge.Task.Factory;
using Game.Context;
using Mooresmaster.Model.ChallengesModule;
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
                foreach (var challengeGuid in MasterHolder.ChallengeMaster.InitialChallenge)
                {
                    var challenge = MasterHolder.ChallengeMaster.GetChallenge(challengeGuid);
                    var initialChallenge = CreateChallenge(playerId, challenge);
                    initialChallenges.Add(initialChallenge);
                }
                
                return new PlayerChallengeInfo(initialChallenges, new List<Guid>());
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
                foreach (var initialChallengeGuid in MasterHolder.ChallengeMaster.InitialChallenge)
                {
                    // クリア済みならスキップ
                    if (challengeJsonObject.CompletedGuids.Contains(initialChallengeGuid.ToString())) continue;
                    
                    var challenge = MasterHolder.ChallengeMaster.GetChallenge(initialChallengeGuid);
                    var initialChallenge = CreateChallenge(playerId, challenge);
                    currentChallenges.Add(initialChallenge);
                }
                
                // CurrentChallengeを作成
                foreach (var completedId in challengeJsonObject.CompletedGuids)
                {
                    // 完了したチャレンジの次のチャレンジがクリア済みでなければ、CurrentChallengeに追加
                    //var challenge = MasterHolder.ChallengeMaster.GetChallenge();
                    
                    var nextIds = MasterHolder.ChallengeMaster.GetNextChallenges(Guid.Parse(completedId));
                    foreach (var nextId in nextIds)
                    {
                        if (challengeJsonObject.CompletedGuids.Contains(nextId.ToString())) continue;
                        
                        var challengeElement = MasterHolder.ChallengeMaster.GetChallenge(nextId);
                        var initialChallenge = CreateChallenge(playerId, challengeElement);
                        currentChallenges.Add(initialChallenge);
                    }
                }
                
                var completedChallengeIds =  challengeJsonObject.CompletedGuids.ConvertAll(Guid.Parse);
                _playerChallengeInfos.Add(playerId, new PlayerChallengeInfo(currentChallenges, completedChallengeIds));
            }
        }
        
        private IChallengeTask CreateChallenge(int playerId, ChallengeElement challengeElement)
        {
            var challenge = _challengeFactory.CreateChallengeTask(playerId, challengeElement);
            challenge.OnChallengeComplete.Subscribe(CompletedChallenge);
            return challenge;
        }
        
        private void CompletedChallenge(IChallengeTask currentChallenge)
        {
            var playerId = currentChallenge.PlayerId;
            var challengeInfo = _playerChallengeInfos[playerId];
            
            challengeInfo.CurrentChallenges.Remove(currentChallenge);
            challengeInfo.CompletedChallengeGuids.Add(currentChallenge.ChallengeElement.ChallengeGuid);
            
            var nextIds = MasterHolder.ChallengeMaster.GetNextChallenges(currentChallenge.ChallengeElement.ChallengeGuid);
            foreach (var nextId in nextIds)
            {
                var challengeElement = MasterHolder.ChallengeMaster.GetChallenge(nextId);
                
                var nextChallenge = CreateChallenge(playerId, challengeElement);
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
                var completedIds = challengeInfo.Value.CompletedChallengeGuids.Select(x => x.ToString()).ToList();
                
                result.Add(new ChallengeJsonObject
                {
                    PlayerId = playerId,
                    CompletedGuids = completedIds
                });
            }
            
            return result;
        }
    }
    
    public class PlayerChallengeInfo
    {
        public List<IChallengeTask> CurrentChallenges { get; }
        public List<Guid> CompletedChallengeGuids { get; }
        
        public PlayerChallengeInfo(List<IChallengeTask> currentChallenges, List<Guid> completedChallengeGuids)
        {
            CurrentChallenges = currentChallenges;
            CompletedChallengeGuids = completedChallengeGuids;
        }
    }
}