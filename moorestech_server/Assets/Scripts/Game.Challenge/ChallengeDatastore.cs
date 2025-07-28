using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Challenge.Task;
using Game.Challenge.Task.Factory;
using Game.UnlockState;
using Mooresmaster.Model.ChallengeActionModule;
using Mooresmaster.Model.ChallengesModule;
using UniRx;

namespace Game.Challenge
{
    public class ChallengeDatastore
    {
        public CurrentChallengeInfo CurrentChallengeInfo { get; private set; } = new();
        
        private readonly IGameUnlockStateDataController _gameUnlockStateDataController;
        private readonly ChallengeEvent _challengeEvent;
        private readonly ChallengeFactory _challengeFactory = new();
        
        
        public ChallengeDatastore(IGameUnlockStateDataController gameUnlockStateDataController, ChallengeEvent challengeEvent)
        {
            _gameUnlockStateDataController = gameUnlockStateDataController;
            _challengeEvent = challengeEvent;
            GameUpdater.UpdateObservable.Subscribe(Update);
        }
        
        private void Update(Unit unit)
        {
            for (var i = CurrentChallengeInfo.CurrentChallenges.Count - 1; i >= 0; i--)
            {
                var currentChallenge = CurrentChallengeInfo.CurrentChallenges[i];
                currentChallenge.ManualUpdate();
            }
        }
        
        
        private IChallengeTask CreateChallenge(ChallengeMasterElement challengeElement)
        {
            var challenge = _challengeFactory.CreateChallengeTask(challengeElement);
            challenge.OnChallengeComplete.Subscribe(CompletedChallenge);
            return challenge;
        }
        
        private void CompletedChallenge(IChallengeTask currentChallenge)
        {
            // クリアしたチャレンジを削除
            // Remove the cleared challenge
            CurrentChallengeInfo.CurrentChallenges.Remove(currentChallenge);
            
            // クリア済みに登録されていればスキップ
            // Skip if already registered as cleared
            var isAlreadyCompleted = CurrentChallengeInfo.CompletedChallengeGuids.Contains(currentChallenge.ChallengeMasterElement.ChallengeGuid);
            if (isAlreadyCompleted) return;
            
            // クリア済みに登録
            // Register as cleared
            CurrentChallengeInfo.CompletedChallengeGuids.Add(currentChallenge.ChallengeMasterElement.ChallengeGuid);
            
            UnlockAllPreviousChallengeComplete(CurrentChallengeInfo, currentChallenge.ChallengeMasterElement);
            
            // クリア時のアクションを実行。次のチャレンジを登録する際にチャレンジのアンロックが走るため、先にクリアアクションを実行する
            // Perform the action when cleared. When registering the next challenge, the challenge unlock will run, so execute the cleared action first
            ExecuteChallengeActions(currentChallenge.ChallengeMasterElement.ClearedActions.items);
            
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
                    var isCompleted = IsChallengesCompleted(CurrentChallengeInfo, challengeElement);
                    if (!isCompleted) continue;
                    
                    // 現在のチャレンジとして登録
                    // Register as a current challenge
                    var nextChallenge = CreateChallenge(challengeElement);
                    CurrentChallengeInfo.CurrentChallenges.Add(nextChallenge);
                    addedNextChallenges.Add(nextChallengeMaster);
                    
                    // チャレンジスタートのアクションを実行する
                    ExecuteChallengeActions(nextChallengeMaster.StartedActions.items);
                }
                
                // イベントを発行
                // Issue an event
                _challengeEvent.InvokeCompleteChallenge(currentChallenge, addedNextChallenges, CurrentChallengeInfo.PlayedSkitIds);
            }
            
            void ExecuteChallengeActions(ChallengeActionElement[] actions)
            {
                foreach (var action in actions)
                {
                    ExecuteClearedAction(action);
                }
            }
            
            #endregion
        }
        
        #region SaveLoad
        
        public void LoadChallenge(ChallengeJsonObject challengeJsonObject)
        {
            var currentChallenges = new List<IChallengeTask>();
            
            // 完了したチャレンジのGUIDリストを作成
            // Create a list of completed challenge GUIDs
            var completedChallengeIds = challengeJsonObject.CompletedGuids.ConvertAll(Guid.Parse);
            var playerChallengeInfo = new CurrentChallengeInfo(new List<IChallengeTask>(), completedChallengeIds);
            
            // 前チャレンジがクリアしているときにアンロックする処理
            // Unlock process when the previous challenge is cleared
            AutoUnlockChallenge(playerChallengeInfo);
            
            // 新規で追加されたレシピやアイテムをアンロックするため、ロードのたびにアンロック系クリアアクションを実行
            // To unlock newly added recipes and items, perform unlock clear actions every time you load the game.
            ExecuteUnlockActionsOnLoad(completedChallengeIds);
            
            // CurrentChallengeを作成
            // create current challenges
            CreateCurrentChallenge(currentChallenges, challengeJsonObject.CurrentChallengeGuids);
            
            CurrentChallengeInfo = new CurrentChallengeInfo(currentChallenges, completedChallengeIds);
            
            #region Internal
            
            void AutoUnlockChallenge(CurrentChallengeInfo challengeInfo)
            {
                foreach (var completedGuid in challengeInfo.CompletedChallengeGuids)
                {
                    var challengeElement = MasterHolder.ChallengeMaster.GetChallenge(completedGuid);
                    UnlockAllPreviousChallengeComplete(challengeInfo, challengeElement);
                }
            }
            
            void CreateCurrentChallenge(List<IChallengeTask> currentChallenges, List<string> currentChallengeGuidStrings)
            {
                // JSONからロードされたCurrentChallengeGuidを元に、現在挑戦中のチャレンジを再構築する
                
                var currentChallengeGuids = currentChallengeGuidStrings.ConvertAll(Guid.Parse);
                foreach (var currentChallenge in currentChallengeGuids)
                {
                    var challengeElement = MasterHolder.ChallengeMaster.GetChallenge(currentChallenge);
                    if (challengeElement == null) continue;
                    
                    
                    var next = CreateChallenge(challengeElement);
                    if (currentChallenges.Any(c => c.ChallengeMasterElement.ChallengeGuid == next.ChallengeMasterElement.ChallengeGuid)) continue;
                    currentChallenges.Add(next);
                    
                    // 新たにマスタで追加されたチャレンジの可能性もあるため、アンロック系だけ実行しておく
                    // There may be new challenges added by the master, so only run the unlocking ones.
                    ExecuteUnlockActions(next.ChallengeMasterElement.StartedActions.items);
                }
            }
            
            void ExecuteUnlockActionsOnLoad(List<Guid> completedChallengeGuids)
            {
                foreach (var completedGuid in completedChallengeGuids)
                {
                    var challengeElement = MasterHolder.ChallengeMaster.GetChallenge(completedGuid);
                    ExecuteUnlockActions(challengeElement.ClearedActions.items);
                }
            }
            
            void ExecuteUnlockActions(ChallengeActionElement[] actions)
            {
                foreach (var action in actions)
                {
                    switch (action.ChallengeActionType)
                    {
                        case ChallengeActionElement.ChallengeActionTypeConst.unlockCraftRecipe:
                        case ChallengeActionElement.ChallengeActionTypeConst.unlockItemRecipeView:
                        case ChallengeActionElement.ChallengeActionTypeConst.unlockChallenge:
                            ExecuteClearedAction(action);
                            break;
                    }
                }
            }
            
#endregion
        }
        
        public ChallengeJsonObject GetSaveJsonObject()
        {
            var completedIds = CurrentChallengeInfo.CompletedChallengeGuids.Select(x => x.ToString()).ToList();
            var currentChallengeGuids = CurrentChallengeInfo.CurrentChallenges.Select(x => x.ChallengeMasterElement.ChallengeGuid.ToString()).ToList();
            
            return new ChallengeJsonObject
            {
                CompletedGuids = completedIds,
                CurrentChallengeGuids = currentChallengeGuids,
                PlayedSkitIds = CurrentChallengeInfo.PlayedSkitIds,
            };
        }
        
        #endregion
        
        
        
        /// <summary>
        /// チャレンジがコンプリートできるかをチェック
        /// Check if the challenge can be completed
        /// </summary>
        private bool IsChallengesCompleted(CurrentChallengeInfo challengeInfo, ChallengeMasterElement challengeElement)
        {
            // チャレンジがアンロックされていない場合はクリアできない
            // If the challenge is not unlocked, it cannot be cleared
            var isUnlocked = _gameUnlockStateDataController.ChallengeCategoryUnlockStateInfos[challengeElement.ChallengeGuid].IsUnlocked;
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
        
        private void UnlockAllPreviousChallengeComplete(CurrentChallengeInfo challengeInfo, ChallengeMasterElement completedChallenge)
        {
            var nextChallenges = MasterHolder.ChallengeMaster.GetNextChallenges(completedChallenge.ChallengeGuid);
            foreach (var nextChallenge in nextChallenges)
            {
                if (!CheckUnlockChallenge(nextChallenge)) continue;
                
                _gameUnlockStateDataController.UnlockChallenge(nextChallenge.ChallengeGuid);
            }
            
            return;
            
            #region Internal
            
            bool CheckUnlockChallenge(ChallengeMasterElement nextChallenge)
            {
                // アンロックを行うかをチェック
                // Check if unlocking is to be performed
                if (!nextChallenge.UnlockAllPreviousChallengeComplete) return false;
                
                
                // 前提条件がない場合は常にアンロック
                // If there are no prerequisites, it can always be started
                if (nextChallenge.PrevChallengeGuids == null || nextChallenge.PrevChallengeGuids.Length == 0)
                {
                    return false;
                }
                
                // すべての前提条件がクリア済みかチェック
                // Check if all prerequisites have been cleared
                foreach (var prevGuid in nextChallenge.PrevChallengeGuids)
                {
                    if (!challengeInfo.CompletedChallengeGuids.Contains(prevGuid))
                    {
                        // クリアしてないチャレンジがあったので、アンロックしない
                        // If there was a challenge that was not cleared, do not unlock
                        return false;
                    }
                }
                
                return true;
            }
            
            #endregion
        }
        
        private void ExecuteClearedAction(ChallengeActionElement action)
        {
            switch (action.ChallengeActionType)
            {
                case ChallengeActionElement.ChallengeActionTypeConst.unlockCraftRecipe:
                    var unlockRecipeGuids = ((UnlockCraftRecipeChallengeActionParam) action.ChallengeActionParam).UnlockRecipeGuids;
                    foreach (var guid in unlockRecipeGuids)
                    {
                        _gameUnlockStateDataController.UnlockCraftRecipe(guid);
                    }
                    break;
                case ChallengeActionElement.ChallengeActionTypeConst.unlockItemRecipeView:
                    var itemGuids = ((UnlockItemRecipeViewChallengeActionParam) action.ChallengeActionParam).UnlockItemGuids;
                    foreach (var itemGuid in itemGuids)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemId(itemGuid);
                        _gameUnlockStateDataController.UnlockItem(itemId);
                    }
                    break;
                case ChallengeActionElement.ChallengeActionTypeConst.unlockChallenge:
                    var challenges = ((UnlockChallengeChallengeActionParam) action.ChallengeActionParam).UnlockChallengeGuids;
                    foreach (var guid in challenges)
                    {
                        _gameUnlockStateDataController.UnlockChallenge(guid);
                    }
                    break;
            }
        }
    }
    
    public class CurrentChallengeInfo
    {
        public List<IChallengeTask> CurrentChallenges { get; }
        public List<Guid> CompletedChallengeGuids { get; }
        public List<string> PlayedSkitIds { get; }
        
        public CurrentChallengeInfo(List<IChallengeTask> currentChallenges, List<Guid> completedChallengeGuids)
        {
            CurrentChallenges = currentChallenges;
            CompletedChallengeGuids = completedChallengeGuids;
            PlayedSkitIds = new List<string>();
        }
        
        public CurrentChallengeInfo()
        {
            CurrentChallenges = new List<IChallengeTask>();
            CompletedChallengeGuids = new List<Guid>();
            PlayedSkitIds = new List<string>();
        }
    }
}