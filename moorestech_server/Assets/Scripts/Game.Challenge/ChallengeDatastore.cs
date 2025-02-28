using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Challenge.Task;
using Game.Challenge.Task.Factory;
using Game.Context;
using Game.UnlockState;
using Mooresmaster.Model.ChallengesModule;
using UniRx;

namespace Game.Challenge
{
    public class ChallengeDatastore
    {
        private readonly IGameUnlockStateDataController gameUnlockStateDataController;
        private ChallengeEvent _challengeEvent;
        
        private readonly Dictionary<int, PlayerChallengeInfo> _playerChallengeInfos = new();
        private readonly ChallengeFactory _challengeFactory = new();
        
        public ChallengeDatastore(IGameUnlockStateDataController gameUnlockStateDataController, ChallengeEvent challengeEvent)
        {
            this.gameUnlockStateDataController = gameUnlockStateDataController;
            _challengeEvent = challengeEvent;
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
        
        
        private IChallengeTask CreateChallenge(int playerId, ChallengeMasterElement challengeElement)
        {
            var challenge = _challengeFactory.CreateChallengeTask(playerId, challengeElement);
            challenge.OnChallengeComplete.Subscribe(CompletedChallenge);
            return challenge;
        }
        
        private void CompletedChallenge(IChallengeTask currentChallenge)
        {
            var playerId = currentChallenge.PlayerId;
            var challengeInfo = _playerChallengeInfos[playerId];
            
            // クリアしたチャレンジを削除
            // Remove the cleared challenge
            challengeInfo.CurrentChallenges.Remove(currentChallenge);
            challengeInfo.CompletedChallengeGuids.Add(currentChallenge.ChallengeMasterElement.ChallengeGuid);
            
            // 次のチャレンジを登録
            // Register the next challenge
            var nextChallenges = MasterHolder.ChallengeMaster.GetNextChallenges(currentChallenge.ChallengeMasterElement.ChallengeGuid);
            foreach (var nextChallengeMaster in nextChallenges)
            {
                var challengeElement = MasterHolder.ChallengeMaster.GetChallenge(nextChallengeMaster.ChallengeGuid);
                
                var nextChallenge = CreateChallenge(playerId, challengeElement);
                challengeInfo.CurrentChallenges.Add(nextChallenge);
            }
            
            // イベントを発行
            // Issue an event
            _challengeEvent.InvokeCompleteChallenge(currentChallenge, nextChallenges);
            
            // クリア時のアクションを実行
            // Perform the action when cleared
            foreach (var action in currentChallenge.ChallengeMasterElement.ClearedActions)
            {
                switch (action.ClearedActionType)
                {
                    case ClearedActionsElement.ClearedActionTypeConst.unlockCraftRecipe:
                        var unlockRecipeGuid = ((UnlockCraftRecipeClearedActionParam) action.ClearedActionParam).UnlockRecipeGuid;
                        gameUnlockStateDataController.UnlockCraftRecipe(unlockRecipeGuid);
                        break;
                    case ClearedActionsElement.ClearedActionTypeConst.unlockItemRecipeView:
                        var itemGuid = ((UnlockItemRecipeViewClearedActionParam) action.ClearedActionParam).UnlockItemGuid;
                        var itemId = MasterHolder.ItemMaster.GetItemId(itemGuid);
                        gameUnlockStateDataController.UnlockItem(itemId);
                        break;
                }
            }
        }
        
        #region SaveLoad
        
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
                    
                    var nextChallenges = MasterHolder.ChallengeMaster.GetNextChallenges(Guid.Parse(completedId));
                    foreach (var nextChallenge in nextChallenges)
                    {
                        if (challengeJsonObject.CompletedGuids.Contains(nextChallenge.ToString())) continue;
                        
                        var challengeElement = MasterHolder.ChallengeMaster.GetChallenge(nextChallenge.ChallengeGuid);
                        var initialChallenge = CreateChallenge(playerId, challengeElement);
                        currentChallenges.Add(initialChallenge);
                    }
                }
                
                var completedChallengeIds =  challengeJsonObject.CompletedGuids.ConvertAll(Guid.Parse);
                _playerChallengeInfos.Add(playerId, new PlayerChallengeInfo(currentChallenges, completedChallengeIds));
            }
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
        
        #endregion
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