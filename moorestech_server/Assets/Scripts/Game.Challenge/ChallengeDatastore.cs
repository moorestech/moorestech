using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Challenge.Task;
using Game.Challenge.Task.Factory;
using Game.UnlockState;
using Mooresmaster.Model.ChallengesModule;
using UniRx;

namespace Game.Challenge
{
    public class ChallengeDatastore
    {
        private readonly IGameUnlockStateDataController _gameUnlockStateDataController;
        private readonly ChallengeEvent _challengeEvent;
        
        private readonly Dictionary<int, PlayerChallengeInfo> _playerChallengeInfos = new();
        private readonly ChallengeFactory _challengeFactory = new();
        
        public ChallengeDatastore(IGameUnlockStateDataController gameUnlockStateDataController, ChallengeEvent challengeEvent)
        {
            _gameUnlockStateDataController = gameUnlockStateDataController;
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
            
            // クリア済みに登録されていればスキップ
            // Skip if already registered as cleared
            var isAlreadyCompleted = challengeInfo.CompletedChallengeGuids.Contains(currentChallenge.ChallengeMasterElement.ChallengeGuid);
            if (isAlreadyCompleted) return;
            
            // クリア済みに登録
            // Register as cleared
            challengeInfo.CompletedChallengeGuids.Add(currentChallenge.ChallengeMasterElement.ChallengeGuid);
            
            // クリア時のアクションを実行。次のチャレンジを登録する際にチャレンジのアンロックが走るため、先にクリアアクションを実行する
            // Perform the action when cleared. When registering the next challenge, the challenge unlock will run, so execute the cleared action first
            ExecuteClearedActions();
            
            // 次のチャレンジを登録
            // Register the next challenge
            RegisterNextChallenge();
            
            #region Internal
            
            void RegisterNextChallenge()
            {
                var masterNextChallenges = MasterHolder.ChallengeMaster.GetNextChallenges(currentChallenge.ChallengeMasterElement.ChallengeGuid);
                var addedNextChallenges = new List<ChallengeMasterElement>();
                foreach (var nextChallengeMaster in masterNextChallenges)
                {
                    var challengeElement = MasterHolder.ChallengeMaster.GetChallenge(nextChallengeMaster.ChallengeGuid);
                    
                    // 前提条件となるチャレンジがすべてクリア済みか、かつ、チャレンジがアンロックされているかチェック
                    // Check if all prerequisite challenges have been cleared AND the challenge is unlocked
                    var isCompleted = IsChallengesCompleted(challengeInfo, challengeElement);
                    if (!isCompleted) continue;
                    
                    var nextChallenge = CreateChallenge(playerId, challengeElement);
                    challengeInfo.CurrentChallenges.Add(nextChallenge);
                    addedNextChallenges.Add(nextChallengeMaster);
                }
                
                // イベントを発行
                // Issue an event
                _challengeEvent.InvokeCompleteChallenge(currentChallenge, addedNextChallenges);
            }
            
            void ExecuteClearedActions()
            {
                foreach (var action in currentChallenge.ChallengeMasterElement.ClearedActions)
                {
                    ExecuteClearedAction(action);
                }
            }
            
  #endregion
        }
        
        #region SaveLoad
        
        public void LoadChallenge(List<ChallengeJsonObject> challengeJsonObjects)
        {
            foreach (var challengeJsonObject in challengeJsonObjects)
            {
                var playerId = challengeJsonObject.PlayerId;
                var currentChallenges = new List<IChallengeTask>();
                
                // InitialChallengeの中でクリアしていないのを登録
                // Register initial challenges that have not been cleared
                RegisterInitialChallenge(challengeJsonObject, currentChallenges, playerId);
                
                // 完了したチャレンジのGUIDリストを作成
                // Create a list of completed challenge GUIDs
                var completedChallengeIds = challengeJsonObject.CompletedGuids.ConvertAll(Guid.Parse);
                var playerChallengeInfo = new PlayerChallengeInfo(new List<IChallengeTask>(), completedChallengeIds);
                
                // 完了済みのチャレンジに対応するアンロック系クリアアクションを実行
                // Execute unlock-related clear actions corresponding to completed challenges
                ExecuteUnlockActionsOnLoad(completedChallengeIds);
                
                // CurrentChallengeを作成
                // create current challenges
                CreateCurrentChallenge(completedChallengeIds, currentChallenges, playerChallengeInfo, playerId);
                
                _playerChallengeInfos.Add(playerId, new PlayerChallengeInfo(currentChallenges, completedChallengeIds));
            }
            
            #region Internal
            
            void RegisterInitialChallenge(ChallengeJsonObject challengeJsonObject, List<IChallengeTask> currentChallenges, int playerId)
            {
                foreach (var initialChallengeGuid in MasterHolder.ChallengeMaster.InitialChallenge)
                {
                    // クリア済みならスキップ
                    if (challengeJsonObject.CompletedGuids.Contains(initialChallengeGuid.ToString())) continue;
                    
                    var challenge = MasterHolder.ChallengeMaster.GetChallenge(initialChallengeGuid);
                    // 初期チャレンジもアンロック状態を確認 (通常はアンロックされているはず)
                    // Check unlock state for initial challenges as well (usually unlocked)
                    var isUnlocked = _gameUnlockStateDataController.ChallengeUnlockStateInfos[challenge.ChallengeGuid].IsUnlocked;
                    if (isUnlocked)
                    {
                        var initialChallenge = CreateChallenge(playerId, challenge);
                        currentChallenges.Add(initialChallenge);
                    }
                }
            }
            
            void CreateCurrentChallenge(List<Guid> completeChallenges, List<IChallengeTask> currentChallenges, PlayerChallengeInfo playerChallengeInfo, int playerId)
            {
                foreach (var completedId in completeChallenges)
                {
                    // 完了したチャレンジの次のチャレンジがクリア済みでなければ、CurrentChallengeに追加
                    var nextChallenges = MasterHolder.ChallengeMaster.GetNextChallenges(completedId);
                    foreach (var nextChallenge in nextChallenges)
                    {
                        if (completeChallenges.Contains(nextChallenge.ChallengeGuid)) continue;
                        
                        var challengeElement = MasterHolder.ChallengeMaster.GetChallenge(nextChallenge.ChallengeGuid);
                        
                        // 前提条件となるチャレンジがすべてクリア済みか、かつ、チャレンジがアンロックされているかチェック
                        // Check if all prerequisite challenges have been cleared AND the challenge is unlocked
                        if (!IsChallengesCompleted(playerChallengeInfo, challengeElement)) continue;
                        
                        var next = CreateChallenge(playerId, challengeElement);
                        // 既にcurrentChallengesに含まれていないか確認してから追加
                        if (currentChallenges.All(c => c.ChallengeMasterElement.ChallengeGuid != next.ChallengeMasterElement.ChallengeGuid))
                        {
                            currentChallenges.Add(next);
                        }
                    }
                }
            }
            
            void ExecuteUnlockActionsOnLoad(List<Guid> completedChallengeGuids)
            {
                foreach (var completedGuid in completedChallengeGuids)
                {
                    var challengeElement = MasterHolder.ChallengeMaster.GetChallenge(completedGuid);
                    
                    foreach (var action in challengeElement.ClearedActions)
                    {
                        switch (action.ClearedActionType)
                        {
                            case ClearedActionsElement.ClearedActionTypeConst.unlockCraftRecipe:
                            case ClearedActionsElement.ClearedActionTypeConst.unlockItemRecipeView:
                            case ClearedActionsElement.ClearedActionTypeConst.unlockChallenge:
                                ExecuteClearedAction(action);
                                break;
                        }
                    }
                }
            }
            
#endregion
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
        
        
        
        /// <summary>
        /// チャレンジがコンプリートできるかをチェック
        /// Check if the challenge can be completed
        /// </summary>
        private bool IsChallengesCompleted(PlayerChallengeInfo challengeInfo, ChallengeMasterElement challengeElement)
        {
            // チャレンジがアンロックされていない場合はクリアできない
            // If the challenge is not unlocked, it cannot be cleared
            var isUnlocked = _gameUnlockStateDataController.ChallengeUnlockStateInfos[challengeElement.ChallengeGuid].IsUnlocked;
            if (!isUnlocked) return false;
            
            // 前提条件がない場合は常に開始可能
            // If there are no prerequisites, it can always be started
            if (challengeElement.PrevChallengeGuids == null || challengeElement.PrevChallengeGuids.Length == 0)
                return true;
            
            // すべての前提条件がクリア済みかチェック
            // Check if all prerequisites have been cleared
            foreach (var prevGuid in challengeElement.PrevChallengeGuids)
            {
                if (!challengeInfo.CompletedChallengeGuids.Contains(prevGuid))
                    return false;
            }
            
            return true;
        }
        
        private void ExecuteClearedAction(ClearedActionsElement action)
        {
            switch (action.ClearedActionType)
            {
                case ClearedActionsElement.ClearedActionTypeConst.unlockCraftRecipe:
                    var unlockRecipeGuids = ((UnlockCraftRecipeClearedActionParam) action.ClearedActionParam).UnlockRecipeGuids;
                    foreach (var guid in unlockRecipeGuids)
                    {
                        _gameUnlockStateDataController.UnlockCraftRecipe(guid);
                    }
                    break;
                case ClearedActionsElement.ClearedActionTypeConst.unlockItemRecipeView:
                    var itemGuids = ((UnlockItemRecipeViewClearedActionParam) action.ClearedActionParam).UnlockItemGuids;
                    foreach (var itemGuid in itemGuids)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemId(itemGuid);
                        _gameUnlockStateDataController.UnlockItem(itemId);
                    }
                    break;
                case ClearedActionsElement.ClearedActionTypeConst.unlockChallenge:
                    var unlockChallengeParam = (UnlockChallengeClearedActionParam) action.ClearedActionParam;
                    foreach (var guid in unlockChallengeParam.UnlockChallengeGuids)
                    {
                        _gameUnlockStateDataController.UnlockChallenge(guid);
                    }
                    break;
            }
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