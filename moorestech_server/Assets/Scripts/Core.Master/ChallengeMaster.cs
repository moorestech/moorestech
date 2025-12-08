using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Loader.ChallengesModule;
using Mooresmaster.Model.ChallengesModule;
using Mooresmaster.Model.GameActionModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class ChallengeMaster
    {
        public readonly Challenges Challenges;
        public ChallengeCategoryMasterElement[] ChallengeCategoryMasterElements => Challenges.Data;
        
        private readonly Dictionary<Guid, ChallengeCategoryMasterElement> _challengeCategoryGuidMap = new();
        private readonly Dictionary<Guid, ChallengeMasterElement> _challengeGuidMap = new();
        private readonly Dictionary<Guid, ChallengeCategoryMasterElement> _challengeToCategoryMap = new();
        private readonly Dictionary<Guid, List<Guid>> _nextChallenges;
        
        public ChallengeMaster(JToken challengeJToken)
        {
            Challenges = ChallengesLoader.Load(challengeJToken);
            _nextChallenges = new Dictionary<Guid, List<Guid>>();
            foreach (var challengeCategory in Challenges.Data)
            {
                _challengeCategoryGuidMap.Add(challengeCategory.CategoryGuid, challengeCategory);
                foreach (var challengeElement in challengeCategory.Challenges)
                {
                    var next = new List<Guid>();
                    foreach (var checkTarget in challengeCategory.Challenges)
                    {
                        var prev = checkTarget.PrevChallengeGuids;
                        if (prev != null && prev.Contains(challengeElement.ChallengeGuid))
                        {
                            next.Add(checkTarget.ChallengeGuid);
                        }
                    }
                    
                    _nextChallenges.Add(challengeElement.ChallengeGuid, next);
                    _challengeGuidMap.Add(challengeElement.ChallengeGuid, challengeElement);
                    _challengeToCategoryMap.Add(challengeElement.ChallengeGuid, challengeCategory);
                }
            }

            // 外部キーバリデーション
            // Foreign key validation
            CategoryIconValidation();
            TaskParamValidation();
            TutorialValidation();
            PrevChallengeValidation();
            GameActionValidation();

            #region Internal

            void CategoryIconValidation()
            {
                var errorLogs = "";
                foreach (var category in Challenges.Data)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(category.IconItem);
                    if (itemId == null)
                    {
                        errorLogs += $"[ChallengeMaster] Category:{category.CategoryName} has invalid IconItem:{category.IconItem}\n";
                    }
                }

                if (!string.IsNullOrEmpty(errorLogs))
                {
                    throw new Exception(errorLogs);
                }
            }

            void TaskParamValidation()
            {
                var errorLogs = "";
                foreach (var category in Challenges.Data)
                {
                    foreach (var challenge in category.Challenges)
                    {
                        switch (challenge.TaskParam)
                        {
                            case CreateItemTaskParam createItem:
                            {
                                var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(createItem.ItemGuid);
                                if (itemId == null)
                                {
                                    errorLogs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid TaskParam.ItemGuid:{createItem.ItemGuid}\n";
                                }
                                break;
                            }
                            case InInventoryItemTaskParam inInventory:
                            {
                                var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(inInventory.ItemGuid);
                                if (itemId == null)
                                {
                                    errorLogs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid TaskParam.ItemGuid:{inInventory.ItemGuid}\n";
                                }
                                break;
                            }
                            case BlockPlaceTaskParam blockPlace:
                            {
                                var blockId = MasterHolder.BlockMaster.GetBlockIdOrNull(blockPlace.BlockGuid);
                                if (blockId == null)
                                {
                                    errorLogs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid TaskParam.BlockGuid:{blockPlace.BlockGuid}\n";
                                }
                                break;
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(errorLogs))
                {
                    throw new Exception(errorLogs);
                }
            }

            void TutorialValidation()
            {
                var errorLogs = "";
                foreach (var category in Challenges.Data)
                {
                    foreach (var challenge in category.Challenges)
                    {
                        foreach (var tutorial in challenge.Tutorials)
                        {
                            switch (tutorial.TutorialParam)
                            {
                                case MapObjectPinTutorialParam mapObjectPin:
                                {
                                    var mapObject = MasterHolder.MapObjectMaster.GetMapObjectElement(mapObjectPin.MapObjectGuid);
                                    if (mapObject == null)
                                    {
                                        errorLogs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid Tutorial.MapObjectGuid:{mapObjectPin.MapObjectGuid}\n";
                                    }
                                    break;
                                }
                                case ItemViewHighLightTutorialParam itemViewHighLight:
                                {
                                    var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(itemViewHighLight.HighLightItemGuid);
                                    if (itemId == null)
                                    {
                                        errorLogs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid Tutorial.HighLightItemGuid:{itemViewHighLight.HighLightItemGuid}\n";
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(errorLogs))
                {
                    throw new Exception(errorLogs);
                }
            }

            void PrevChallengeValidation()
            {
                var errorLogs = "";
                foreach (var category in Challenges.Data)
                {
                    foreach (var challenge in category.Challenges)
                    {
                        if (challenge.PrevChallengeGuids == null) continue;

                        foreach (var prevGuid in challenge.PrevChallengeGuids)
                        {
                            if (!_challengeGuidMap.ContainsKey(prevGuid))
                            {
                                errorLogs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid PrevChallengeGuid:{prevGuid}\n";
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(errorLogs))
                {
                    throw new Exception(errorLogs);
                }
            }

            void GameActionValidation()
            {
                var errorLogs = "";
                foreach (var category in Challenges.Data)
                {
                    foreach (var challenge in category.Challenges)
                    {
                        // StartedActionsのバリデーション
                        // Validate StartedActions
                        errorLogs += ValidateGameActions(challenge.StartedActions.items, challenge.Title, "StartedActions");

                        // ClearedActionsのバリデーション
                        // Validate ClearedActions
                        errorLogs += ValidateGameActions(challenge.ClearedActions.items, challenge.Title, "ClearedActions");
                    }
                }

                if (!string.IsNullOrEmpty(errorLogs))
                {
                    throw new Exception(errorLogs);
                }
            }

            string ValidateGameActions(GameActionElement[] actions, string challengeTitle, string actionType)
            {
                if (actions == null) return "";

                var errorLogs = "";
                foreach (var action in actions)
                {
                    if (action?.GameActionParam == null) continue;

                    switch (action.GameActionParam)
                    {
                        case UnlockCraftRecipeGameActionParam unlockCraftRecipe:
                        {
                            if (unlockCraftRecipe.UnlockRecipeGuids == null) break;
                            foreach (var recipeGuid in unlockCraftRecipe.UnlockRecipeGuids)
                            {
                                var recipe = MasterHolder.CraftRecipeMaster.GetCraftRecipe(recipeGuid);
                                if (recipe == null)
                                {
                                    errorLogs += $"[ChallengeMaster] Challenge:{challengeTitle} {actionType} has invalid UnlockRecipeGuid:{recipeGuid}\n";
                                }
                            }
                            break;
                        }
                        case UnlockItemRecipeViewGameActionParam unlockItemRecipeView:
                        {
                            if (unlockItemRecipeView.UnlockItemGuids == null) break;
                            foreach (var itemGuid in unlockItemRecipeView.UnlockItemGuids)
                            {
                                var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(itemGuid);
                                if (itemId == null)
                                {
                                    errorLogs += $"[ChallengeMaster] Challenge:{challengeTitle} {actionType} has invalid UnlockItemGuid:{itemGuid}\n";
                                }
                            }
                            break;
                        }
                        case UnlockChallengeCategoryGameActionParam unlockChallengeCategory:
                        {
                            if (unlockChallengeCategory.UnlockChallengeCategoryGuids == null) break;
                            foreach (var categoryGuid in unlockChallengeCategory.UnlockChallengeCategoryGuids)
                            {
                                if (!_challengeCategoryGuidMap.ContainsKey(categoryGuid))
                                {
                                    errorLogs += $"[ChallengeMaster] Challenge:{challengeTitle} {actionType} has invalid UnlockChallengeCategoryGuid:{categoryGuid}\n";
                                }
                            }
                            break;
                        }
                        case GiveItemGameActionParam giveItem:
                        {
                            if (giveItem.RewardItems == null) break;
                            foreach (var rewardItem in giveItem.RewardItems)
                            {
                                var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(rewardItem.ItemGuid);
                                if (itemId == null)
                                {
                                    errorLogs += $"[ChallengeMaster] Challenge:{challengeTitle} {actionType} has invalid RewardItem.ItemGuid:{rewardItem.ItemGuid}\n";
                                }
                            }
                            break;
                        }
                    }
                }
                return errorLogs;
            }

            #endregion
        }
        
        public List<ChallengeMasterElement> GetNextChallenges(Guid challengeGuid)
        {
            if (!_nextChallenges.TryGetValue(challengeGuid, out var nextChallenges))
            {
                throw new InvalidOperationException($"Next challenges not found. ChallengeGuid:{challengeGuid}");
            }
            
            return nextChallenges.ConvertAll(GetChallenge);
        }
        
        public ChallengeMasterElement GetChallenge(Guid guid)
        {
            return _challengeGuidMap[guid];
        }
        
        public ChallengeCategoryMasterElement GetChallengeCategoryFromChallengeGuid(Guid guid)
        {
            return _challengeToCategoryMap[guid];
        }
        
        /// <summary>
        /// 指定されたカテゴリの初期チャレンジ（前提条件がないチャレンジ）を取得する
        /// </summary>
        public List<ChallengeMasterElement> GetCategoryInitialChallenges(Guid categoryGuid)
        {
            var category = ChallengeCategoryMasterElements.FirstOrDefault(c => c.CategoryGuid == categoryGuid);
            if (category == null) return new List<ChallengeMasterElement>();
            
            var initialChallenges = new List<ChallengeMasterElement>();
            foreach (var challengeElement in category.Challenges)
            {
                // 前提条件がないチャレンジを初期チャレンジとする
                if (challengeElement.PrevChallengeGuids == null || challengeElement.PrevChallengeGuids.Length == 0)
                {
                    initialChallenges.Add(challengeElement);
                }
            }
            
            return initialChallenges;
        }
        
        public ChallengeCategoryMasterElement GetChallengeCategory(Guid categoryGuid)
        {
            return _challengeCategoryGuidMap[categoryGuid];
        }
    }
}