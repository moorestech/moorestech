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
            
            // カテゴリアンロック時のイベントを購読
            _gameUnlockStateDataController.OnUnlockChallengeCategory.Subscribe(OnChallengeCategoryUnlocked);
        }
        
        public void InitializeCurrentChallenges()
        {
            // 全てのチャレンジカテゴリから初期チャレンジを探す
            CurrentChallengeInfo.CurrentChallenges.AddRange(CollectAndExecuteInitializeChallenges());
        }
        
        private List<IChallengeTask> CollectAndExecuteInitializeChallenges()
        {
            var result = new List<IChallengeTask>();
            foreach (var category in MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements)
            {
                // アンロックされていないカテゴリはスキップ
                if (!_gameUnlockStateDataController.ChallengeCategoryUnlockStateInfos.ContainsKey(category.CategoryGuid)) continue;
                if (!_gameUnlockStateDataController.ChallengeCategoryUnlockStateInfos[category.CategoryGuid].IsUnlocked) continue;
                
                foreach (var challengeElement in category.Challenges)
                {
                    // initialUnlockedがtrueかつ前提条件がないチャレンジを初期チャレンジとする
                    if (challengeElement.PrevChallengeGuids != null && challengeElement.PrevChallengeGuids.Length != 0) continue;
                    
                    var challenge = CreateChallenge(challengeElement);
                    result.Add(challenge);
                    
                    // チャレンジスタートのアクションを実行
                    foreach (var action in challengeElement.StartedActions.items)
                    {
                        ExecuteChallengeAction(action);
                    }
                }
            }
            
            return result;
        }
        
        private void Update(Unit unit)
        {
            for (var i = CurrentChallengeInfo.CurrentChallenges.Count - 1; i >= 0; i--)
            {
                var currentChallenge = CurrentChallengeInfo.CurrentChallenges[i];
                currentChallenge.ManualUpdate();
            }
        }
        
        private void OnChallengeCategoryUnlocked(Guid categoryGuid)
        {
            // カテゴリ内の初期チャレンジを取得
            var initialChallenges = MasterHolder.ChallengeMaster.GetCategoryInitialChallenges(categoryGuid);
            
            foreach (var challengeElement in initialChallenges)
            {
                // 既に現在のチャレンジに含まれているかチェック
                var isAlreadyCurrent = CurrentChallengeInfo.CurrentChallenges
                    .Any(c => c.ChallengeMasterElement.ChallengeGuid == challengeElement.ChallengeGuid);
                if (isAlreadyCurrent) continue;
                
                // 既にクリア済みかチェック
                var isAlreadyCompleted = CurrentChallengeInfo.CompletedChallenges
                    .Any(c => c.ChallengeGuid == challengeElement.ChallengeGuid);
                if (isAlreadyCompleted) continue;
                
                var challenge = CreateChallenge(challengeElement);
                CurrentChallengeInfo.CurrentChallenges.Add(challenge);
                
                // チャレンジスタートのアクションを実行
                foreach (var action in challengeElement.StartedActions.items)
                {
                    ExecuteChallengeAction(action);
                }
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
            var isAlreadyCompleted = CurrentChallengeInfo.CompletedChallenges.Any(c => c.ChallengeGuid == currentChallenge.ChallengeMasterElement.ChallengeGuid);
            if (isAlreadyCompleted) return;
            
            // クリア済みに登録
            // Register as cleared
            CurrentChallengeInfo.CompletedChallenges.Add(currentChallenge.ChallengeMasterElement);
            
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
                    
                    // 前提条件となるチャレンジがすべてクリア済みか
                    // Check if all prerequisite challenges have been cleared
                    var isCompleted = IsChallengesCompleted(CurrentChallengeInfo, challengeElement);
                    if (!isCompleted) continue;
                    
                    // 既に現在のチャレンジに含まれているかチェック
                    // Check if already included in current challenges
                    var isAlreadyCurrent = CurrentChallengeInfo.CurrentChallenges.Any(c => c.ChallengeMasterElement.ChallengeGuid == challengeElement.ChallengeGuid);
                    if (isAlreadyCurrent) continue;
                    
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
                    ExecuteChallengeAction(action);
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
            var completedChallengeElements = new List<ChallengeMasterElement>();
            foreach (var guid in challengeJsonObject.CompletedGuids)
            {
                var challengeElement = MasterHolder.ChallengeMaster.GetChallenge(Guid.Parse(guid));
                if (challengeElement == null) continue;
                
                completedChallengeElements.Add(challengeElement);
                
                // 新規で追加されたレシピやアイテムをアンロックするため、ロードのたびにアンロック系クリアアクションを実行
                // To unlock newly added recipes and items, perform unlock clear actions every time you load the game.
                ExecuteUnlockActions(challengeElement.ClearedActions.items);
            }
            
            
            // CurrentChallengeを作成
            // create current challenges
            CreateCurrentChallenge(currentChallenges, challengeJsonObject.CurrentChallengeGuids);
            
            // CurrentChallengeGuidsが空で、CompletedGuidsも空の場合は初期チャレンジを追加
            // If CurrentChallengeGuids is empty and CompletedGuids is also empty, add initial challenges
            if (challengeJsonObject.CurrentChallengeGuids.Count == 0 && challengeJsonObject.CompletedGuids.Count == 0)
            {
                currentChallenges.AddRange(CollectAndExecuteInitializeChallenges());
            }
            
            CurrentChallengeInfo = new CurrentChallengeInfo(currentChallenges, completedChallengeElements, challengeJsonObject.PlayedSkitIds);
            
            #region Internal
            
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
            
            void ExecuteUnlockActions(ChallengeActionElement[] actions)
            {
                foreach (var action in actions)
                {
                    switch (action.ChallengeActionType)
                    {
                        case ChallengeActionElement.ChallengeActionTypeConst.unlockCraftRecipe:
                        case ChallengeActionElement.ChallengeActionTypeConst.unlockItemRecipeView:
                        case ChallengeActionElement.ChallengeActionTypeConst.unlockChallengeCategory:
                            ExecuteChallengeAction(action);
                            break;
                    }
                }
            }
            
#endregion
        }
        
        public ChallengeJsonObject GetSaveJsonObject()
        {
            var completedIds = CurrentChallengeInfo.CompletedChallenges.Select(x => x.ChallengeGuid.ToString()).ToList();
            var currentChallengeGuids = CurrentChallengeInfo.CurrentChallenges
                .Select(x => x.ChallengeMasterElement.ChallengeGuid.ToString())
                .Distinct()
                .ToList();
            
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
            // 前提条件がない場合は常に開始可能
            // If there are no prerequisites, it can always be started
            if (challengeElement.PrevChallengeGuids == null || challengeElement.PrevChallengeGuids.Length == 0)
                return true;
            
            // すべての前提条件がクリア済みかチェック
            // Check if all prerequisites have been cleared
            foreach (var prevGuid in challengeElement.PrevChallengeGuids)
            {
                if (challengeInfo.CompletedChallenges.All(c => c.ChallengeGuid != prevGuid))
                    return false;
            }
            
            return true;
        }

        
        private void ExecuteChallengeAction(ChallengeActionElement action)
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
                case ChallengeActionElement.ChallengeActionTypeConst.unlockChallengeCategory:
                    var challenges = ((UnlockChallengeCategoryChallengeActionParam) action.ChallengeActionParam).UnlockChallengeCategoryGuids;
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
        public List<ChallengeMasterElement> CompletedChallenges { get; }
        public List<string> PlayedSkitIds { get; }
        
        public CurrentChallengeInfo(List<IChallengeTask> currentChallenges, List<ChallengeMasterElement> completedChallenges, List<string> playedSkitIds)
        {
            CurrentChallenges = currentChallenges;
            CompletedChallenges = completedChallenges;
            PlayedSkitIds = playedSkitIds;
        }
        
        public CurrentChallengeInfo()
        {
            CurrentChallenges = new List<IChallengeTask>();
            CompletedChallenges = new List<ChallengeMasterElement>();
            PlayedSkitIds = new List<string>();
        }
    }
}