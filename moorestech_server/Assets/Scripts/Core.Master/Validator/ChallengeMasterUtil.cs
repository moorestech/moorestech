using System;
using System.Collections.Generic;
using Mooresmaster.Model.ChallengesModule;
using Mooresmaster.Model.GameActionModule;

namespace Core.Master.Validator
{
    public static class ChallengeMasterUtil
    {
        public static bool Validate(Challenges challenges, out string errorLogs)
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
                foreach (var category in challenges.Data)
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
                foreach (var category in challenges.Data)
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
                foreach (var category in challenges.Data)
                {
                    foreach (var challenge in category.Challenges)
                    {
                        foreach (var tutorial in challenge.Tutorials)
                        {
                            switch (tutorial.TutorialParam)
                            {
                                case MapObjectPinTutorialParam mapObjectPin:
                                {
                                    var mapObject = MasterHolder.MapObjectMaster.GetMapObjectElementOrNull(mapObjectPin.MapObjectGuid);
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
                                case BlockPlacePreviewTutorialParam blockPlacePreview:
                                {
                                    // ブロックプレビュー用の配置対象を検証
                                    // Validate target block for placement preview
                                    var blockId = MasterHolder.BlockMaster.GetBlockIdOrNull(blockPlacePreview.BlockGuid);
                                    if (blockId == null)
                                    {
                                        logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid Tutorial.BlockGuid:{blockPlacePreview.BlockGuid}\n";
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
                foreach (var category in challenges.Data)
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
                foreach (var category in challenges.Data)
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
                                var recipe = MasterHolder.CraftRecipeMaster.GetCraftRecipeOrNull(recipeGuid);
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
                foreach (var category in challenges.Data)
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
                return Array.Exists(challenges.Data, c => c.CategoryGuid == categoryGuid);
            }

            #endregion
        }

        public static void Initialize(
            Challenges challenges,
            out Dictionary<Guid, ChallengeCategoryMasterElement> challengeCategoryGuidMap,
            out Dictionary<Guid, ChallengeMasterElement> challengeGuidMap,
            out Dictionary<Guid, ChallengeCategoryMasterElement> challengeToCategoryMap,
            out Dictionary<Guid, List<Guid>> nextChallenges)
        {
            // カテゴリGUIDからカテゴリ要素へのマップを構築
            // Build category GUID to category element map
            challengeCategoryGuidMap = new Dictionary<Guid, ChallengeCategoryMasterElement>();
            foreach (var category in challenges.Data)
            {
                challengeCategoryGuidMap.Add(category.CategoryGuid, category);
            }

            // チャレンジGUIDからチャレンジ要素へのマップを構築
            // Build challenge GUID to challenge element map
            challengeGuidMap = new Dictionary<Guid, ChallengeMasterElement>();
            challengeToCategoryMap = new Dictionary<Guid, ChallengeCategoryMasterElement>();
            foreach (var category in challenges.Data)
            {
                foreach (var challenge in category.Challenges)
                {
                    challengeGuidMap.Add(challenge.ChallengeGuid, challenge);
                    challengeToCategoryMap.Add(challenge.ChallengeGuid, category);
                }
            }

            // 次のチャレンジマップを構築（PrevChallengeGuidsの逆引き）
            // Build next challenges map (reverse lookup of PrevChallengeGuids)
            nextChallenges = new Dictionary<Guid, List<Guid>>();

            // 全チャレンジに対して空のリストを初期化
            // Initialize empty list for all challenges
            foreach (var category in challenges.Data)
            {
                foreach (var challenge in category.Challenges)
                {
                    nextChallenges[challenge.ChallengeGuid] = new List<Guid>();
                }
            }

            // PrevChallengeGuidsから逆引きでNextChallengesを構築
            // Build NextChallenges from reverse lookup of PrevChallengeGuids
            foreach (var category in challenges.Data)
            {
                foreach (var challenge in category.Challenges)
                {
                    if (challenge.PrevChallengeGuids == null) continue;

                    foreach (var prevGuid in challenge.PrevChallengeGuids)
                    {
                        nextChallenges[prevGuid].Add(challenge.ChallengeGuid);
                    }
                }
            }
        }
    }
}
