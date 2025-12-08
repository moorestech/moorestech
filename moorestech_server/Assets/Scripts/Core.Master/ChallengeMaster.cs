using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Loader.ChallengesModule;
using Mooresmaster.Model.ChallengesModule;
using Mooresmaster.Model.GameActionModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class ChallengeMaster : IMasterValidator
    {
        public readonly Challenges Challenges;
        public ChallengeCategoryMasterElement[] ChallengeCategoryMasterElements => Challenges.Data;

        private Dictionary<Guid, ChallengeCategoryMasterElement> _challengeCategoryGuidMap;
        private Dictionary<Guid, ChallengeMasterElement> _challengeGuidMap;
        private Dictionary<Guid, ChallengeCategoryMasterElement> _challengeToCategoryMap;
        private Dictionary<Guid, List<Guid>> _nextChallenges;

        public ChallengeMaster(JToken challengeJToken)
        {
            Challenges = ChallengesLoader.Load(challengeJToken);
        }

        public bool Validate(out string errorLogs)
        {
            errorLogs = "";
            errorLogs += CategoryIconValidation();
            errorLogs += TaskParamValidation();
            errorLogs += TutorialValidation();
            errorLogs += PrevChallengeValidation();
            errorLogs += GameActionValidation();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string CategoryIconValidation()
            {
                var logs = "";
                foreach (var category in Challenges.Data)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(category.IconItem);
                    if (itemId == null)
                    {
                        logs += $"[ChallengeMaster] Category:{category.CategoryName} has invalid IconItem:{category.IconItem}\n";
                    }
                }

                return logs;
            }

            string TaskParamValidation()
            {
                var logs = "";
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
                                    logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid TaskParam.ItemGuid:{createItem.ItemGuid}\n";
                                }
                                break;
                            }
                            case InInventoryItemTaskParam inInventory:
                            {
                                var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(inInventory.ItemGuid);
                                if (itemId == null)
                                {
                                    logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid TaskParam.ItemGuid:{inInventory.ItemGuid}\n";
                                }
                                break;
                            }
                            case BlockPlaceTaskParam blockPlace:
                            {
                                var blockId = MasterHolder.BlockMaster.GetBlockIdOrNull(blockPlace.BlockGuid);
                                if (blockId == null)
                                {
                                    logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid TaskParam.BlockGuid:{blockPlace.BlockGuid}\n";
                                }
                                break;
                            }
                        }
                    }
                }

                return logs;
            }

            string TutorialValidation()
            {
                var logs = "";
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
                                        logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid Tutorial.MapObjectGuid:{mapObjectPin.MapObjectGuid}\n";
                                    }
                                    break;
                                }
                                case ItemViewHighLightTutorialParam itemViewHighLight:
                                {
                                    var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(itemViewHighLight.HighLightItemGuid);
                                    if (itemId == null)
                                    {
                                        logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid Tutorial.HighLightItemGuid:{itemViewHighLight.HighLightItemGuid}\n";
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }

                return logs;
            }

            string PrevChallengeValidation()
            {
                var logs = "";
                foreach (var category in Challenges.Data)
                {
                    foreach (var challenge in category.Challenges)
                    {
                        if (challenge.PrevChallengeGuids == null) continue;

                        foreach (var prevGuid in challenge.PrevChallengeGuids)
                        {
                            if (!ExistsChallengeGuid(prevGuid))
                            {
                                logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid PrevChallengeGuid:{prevGuid}\n";
                            }
                        }
                    }
                }

                return logs;
            }

            string GameActionValidation()
            {
                var logs = "";
                foreach (var category in Challenges.Data)
                {
                    foreach (var challenge in category.Challenges)
                    {
                        // StartedActionsのバリデーション
                        // Validate StartedActions
                        logs += ValidateGameActions(challenge.StartedActions.items, challenge.Title, "StartedActions");

                        // ClearedActionsのバリデーション
                        // Validate ClearedActions
                        logs += ValidateGameActions(challenge.ClearedActions.items, challenge.Title, "ClearedActions");
                    }
                }

                return logs;
            }

            string ValidateGameActions(GameActionElement[] actions, string challengeTitle, string actionType)
            {
                if (actions == null) return "";

                var logs = "";
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
                                    logs += $"[ChallengeMaster] Challenge:{challengeTitle} {actionType} has invalid UnlockRecipeGuid:{recipeGuid}\n";
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
                                    logs += $"[ChallengeMaster] Challenge:{challengeTitle} {actionType} has invalid UnlockItemGuid:{itemGuid}\n";
                                }
                            }
                            break;
                        }
                        case UnlockChallengeCategoryGameActionParam unlockChallengeCategory:
                        {
                            if (unlockChallengeCategory.UnlockChallengeCategoryGuids == null) break;
                            foreach (var categoryGuid in unlockChallengeCategory.UnlockChallengeCategoryGuids)
                            {
                                if (!ExistsCategoryGuid(categoryGuid))
                                {
                                    logs += $"[ChallengeMaster] Challenge:{challengeTitle} {actionType} has invalid UnlockChallengeCategoryGuid:{categoryGuid}\n";
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
                                    logs += $"[ChallengeMaster] Challenge:{challengeTitle} {actionType} has invalid RewardItem.ItemGuid:{rewardItem.ItemGuid}\n";
                                }
                            }
                            break;
                        }
                    }
                }
                return logs;
            }

            bool ExistsChallengeGuid(Guid challengeGuid)
            {
                foreach (var category in Challenges.Data)
                {
                    if (Array.Exists(category.Challenges, c => c.ChallengeGuid == challengeGuid))
                    {
                        return true;
                    }
                }
                return false;
            }

            bool ExistsCategoryGuid(Guid categoryGuid)
            {
                return Array.Exists(Challenges.Data, c => c.CategoryGuid == categoryGuid);
            }

            #endregion
        }

        public void Initialize()
        {
            BuildCategoryGuidMap();
            BuildChallengeGuidMaps();
            BuildNextChallenges();

            #region Internal

            void BuildCategoryGuidMap()
            {
                // カテゴリGUIDからカテゴリ要素へのマップを構築
                // Build category GUID to category element map
                _challengeCategoryGuidMap = new Dictionary<Guid, ChallengeCategoryMasterElement>();
                foreach (var category in Challenges.Data)
                {
                    _challengeCategoryGuidMap.Add(category.CategoryGuid, category);
                }
            }

            void BuildChallengeGuidMaps()
            {
                // チャレンジGUIDからチャレンジ要素へのマップを構築
                // Build challenge GUID to challenge element map
                _challengeGuidMap = new Dictionary<Guid, ChallengeMasterElement>();
                _challengeToCategoryMap = new Dictionary<Guid, ChallengeCategoryMasterElement>();
                foreach (var category in Challenges.Data)
                {
                    foreach (var challenge in category.Challenges)
                    {
                        _challengeGuidMap.Add(challenge.ChallengeGuid, challenge);
                        _challengeToCategoryMap.Add(challenge.ChallengeGuid, category);
                    }
                }
            }

            void BuildNextChallenges()
            {
                // 次のチャレンジマップを構築（PrevChallengeGuidsの逆引き）
                // Build next challenges map (reverse lookup of PrevChallengeGuids)
                _nextChallenges = new Dictionary<Guid, List<Guid>>();

                // 全チャレンジに対して空のリストを初期化
                // Initialize empty list for all challenges
                foreach (var category in Challenges.Data)
                {
                    foreach (var challenge in category.Challenges)
                    {
                        _nextChallenges[challenge.ChallengeGuid] = new List<Guid>();
                    }
                }

                // PrevChallengeGuidsから逆引きでNextChallengesを構築
                // Build NextChallenges from reverse lookup of PrevChallengeGuids
                foreach (var category in Challenges.Data)
                {
                    foreach (var challenge in category.Challenges)
                    {
                        if (challenge.PrevChallengeGuids == null) continue;

                        foreach (var prevGuid in challenge.PrevChallengeGuids)
                        {
                            _nextChallenges[prevGuid].Add(challenge.ChallengeGuid);
                        }
                    }
                }
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