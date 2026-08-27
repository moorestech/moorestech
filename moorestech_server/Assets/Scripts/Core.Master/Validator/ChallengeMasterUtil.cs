using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.ChallengesModule;
using Mooresmaster.Model.GameActionModule;
using Mooresmaster.Model.MapModule;

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
                            case CompleteResearchTaskParam completeResearch:
                            {
                                // 参照先研究ノードの実在を検証
                                // Validate that the referenced research node exists
                                if (MasterHolder.ResearchMaster.GetResearch(completeResearch.ResearchNodeGuid) == null)
                                {
                                    logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid TaskParam.ResearchNodeGuid:{completeResearch.ResearchNodeGuid}\n";
                                }
                                break;
                            }
                            case EquipItemTaskParam equipItem:
                            {
                                var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(equipItem.ItemGuid);
                                if (itemId == null)
                                {
                                    logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid TaskParam.ItemGuid:{equipItem.ItemGuid}\n";
                                }
                                break;
                            }
                            case BlockPlaceOnVeinTaskParam blockPlaceOnVein:
                            {
                                // 対象ブロックと対象鉱脈の両方が実在しないと永久に達成不能になる
                                // The challenge is impossible unless both the block and the vein exist
                                if (MasterHolder.BlockMaster.GetBlockIdOrNull(blockPlaceOnVein.BlockGuid) == null)
                                {
                                    logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid TaskParam.BlockGuid:{blockPlaceOnVein.BlockGuid}\n";
                                }
                                if (MasterHolder.MapVeinMaster.GetElementOrNull(blockPlaceOnVein.VeinGuid) == null)
                                {
                                    logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid TaskParam.VeinGuid:{blockPlaceOnVein.VeinGuid}\n";
                                }
                                break;
                            }
                            case GearConnectedBlockTaskParam gearConnectedBlock:
                            {
                                if (MasterHolder.BlockMaster.GetBlockIdOrNull(gearConnectedBlock.BlockGuid) == null)
                                {
                                    logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid TaskParam.BlockGuid:{gearConnectedBlock.BlockGuid}\n";
                                }
                                break;
                            }
                            default:
                                logs += $"[ChallengeMaster] Challenge:{challenge.Title} has unvalidated TaskParam type:{challenge.TaskParam?.GetType().Name}\n";
                                break;
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
                                    // 狙い先のitemGuidは他5caseと同形にItemMasterで先に突き合わせる
                                    // The target itemGuid is checked against ItemMaster first, matching the other five cases
                                    if (mapObjectPin.PinTargetParam is EarnItemPinTargetParam byEarnItem &&
                                        MasterHolder.ItemMaster.GetItemIdOrNull(byEarnItem.ItemGuid) == null)
                                    {
                                        logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid Tutorial.ItemGuid:{byEarnItem.ItemGuid}\n";
                                        break;
                                    }

                                    // 狙い先の解決規則はChallengeMasterが唯一の持ち主。種別が増えてもこのブロックは変えない
                                    // ChallengeMaster owns the only resolution rule, so adding a target kind never touches this block
                                    if (!MasterHolder.ChallengeMaster.TryResolvePinTargets(mapObjectPin, out var pinTargets))
                                    {
                                        logs += $"[ChallengeMaster] Challenge:{challenge.Title} has unvalidated PinTargetParam type:{mapObjectPin.PinTargetParam?.GetType().Name}\n";
                                        break;
                                    }

                                    // 解決先が空のピンは指す先が永久に現れず達成不能なチュートリアルになる
                                    // A pin resolving to nothing never gets a target, making the tutorial impossible to complete
                                    if (pinTargets.Count == 0)
                                    {
                                        logs += $"[ChallengeMaster] Challenge:{challenge.Title} has a mapObjectPin resolving to no MapObject. PinTargetType:{mapObjectPin.PinTargetType}\n";
                                        break;
                                    }

                                    foreach (var pinTarget in pinTargets)
                                    {
                                        if (MasterHolder.MapObjectMaster.GetMapObjectElementOrNull(pinTarget) == null)
                                        {
                                            logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid Tutorial.MapObjectGuid:{pinTarget}\n";
                                        }
                                    }
                                    break;
                                }
                                case VeinPinTutorialParam veinPin:
                                {
                                    var vein = MasterHolder.MapVeinMaster.GetElementOrNull(veinPin.VeinGuid);
                                    if (vein == null)
                                    {
                                        logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid Tutorial.VeinGuid:{veinPin.VeinGuid}\n";
                                    }
                                    // 手掘りできない鉱脈を指すピンは達成不能なチュートリアルになる
                                    // A pin aimed at an unmineable vein makes the tutorial impossible to complete
                                    else if (vein.HandMiningParam is not MinableHandMiningParam)
                                    {
                                        logs += $"[ChallengeMaster] Challenge:{challenge.Title} points Tutorial.VeinGuid:{veinPin.VeinGuid} which forbids hand mining\n";
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
                                case VeinRestrictedPlacementTutorialParam veinRestricted:
                                {
                                    // 強調と設置許可の対象。どちらが欠けても設置不能なチュートリアルになる
                                    // Both targets gate highlighting and placement; a missing one makes placement impossible
                                    if (MasterHolder.MapVeinMaster.GetElementOrNull(veinRestricted.VeinGuid) == null)
                                    {
                                        logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid Tutorial.VeinGuid:{veinRestricted.VeinGuid}\n";
                                    }
                                    if (MasterHolder.BlockMaster.GetBlockIdOrNull(veinRestricted.BlockGuid) == null)
                                    {
                                        logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid Tutorial.BlockGuid:{veinRestricted.BlockGuid}\n";
                                    }
                                    break;
                                }
                                case RelativeBlockPlacePreviewTutorialParam relativePreview:
                                {
                                    if (MasterHolder.BlockMaster.GetBlockIdOrNull(relativePreview.AnchorBlockGuid) == null)
                                    {
                                        logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid Tutorial.AnchorBlockGuid:{relativePreview.AnchorBlockGuid}\n";
                                    }
                                    if (MasterHolder.BlockMaster.GetBlockIdOrNull(relativePreview.BlockGuid) == null)
                                    {
                                        logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid Tutorial.BlockGuid:{relativePreview.BlockGuid}\n";
                                    }
                                    break;
                                }
                                case UiDragGuideTutorialParam:
                                case UiHighLightTutorialParam:
                                    // anchorIdはWeb側のDOM名乗りと突き合わせるだけなので検証しない（誤設定は表示されないだけ・設定者責任）
                                    // Anchor IDs are only string-matched against web-side DOM declarations; missets simply don't render (configurer's responsibility)
                                    break;
                                case KeyControlTutorialParam:
                                    // uiState/controlTextのみでマスタ参照を持たないため検証対象外
                                    // No master reference to validate (uiState/controlText only)
                                    break;
                                default:
                                    logs += $"[ChallengeMaster] Challenge:{challenge.Title} has unvalidated Tutorial type:{tutorial.TutorialParam?.GetType().Name}\n";
                                    break;
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
                        case UnlockMachineRecipeGameActionParam unlockMachineRecipe:
                        {
                            if (unlockMachineRecipe.UnlockMachineRecipeGuids == null) break;
                            foreach (var machineRecipeGuid in unlockMachineRecipe.UnlockMachineRecipeGuids)
                            {
                                // 機械レシピの参照先が存在することを検証
                                // Validate that the referenced machine recipe exists
                                var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(machineRecipeGuid);
                                if (recipe == null)
                                {
                                    logs += $"[ChallengeMaster] Challenge:{challengeTitle} {actionType} has invalid UnlockMachineRecipeGuid:{machineRecipeGuid}\n";
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
                        case UnlockItemStackLevelGameActionParam unlockItemStackLevel:
                        {
                            // TargetItemGuidsがnullだと実行側が無ガードで走査し実行時NREになるため検証で弾く
                            // Null TargetItemGuids would NRE the unguarded runtime foreach, so reject it in validation
                            if (unlockItemStackLevel.TargetItemGuids == null)
                            {
                                logs += $"[ChallengeMaster] Challenge:{challengeTitle} {actionType} has invalid (null) TargetItemGuids\n";
                                break;
                            }
                            foreach (var itemGuid in unlockItemStackLevel.TargetItemGuids)
                            {
                                // 対象アイテムの実在を検証
                                // Validate that the target item exists
                                var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(itemGuid);
                                if (itemId == null)
                                {
                                    logs += $"[ChallengeMaster] Challenge:{challengeTitle} {actionType} has invalid TargetItemGuid:{itemGuid}\n";
                                    continue;
                                }

                                // 解放レベルがテーブル長範囲内か検証
                                // Validate the unlock level is within the table range
                                var element = MasterHolder.ItemMaster.GetItemMaster(itemId.Value);
                                var table = MasterHolder.ItemMaster.GetStackLevelTable(element.StackLevelTableGuid);
                                if (unlockItemStackLevel.Level < 1 || table.StackCounts.Length < unlockItemStackLevel.Level)
                                {
                                    logs += $"[ChallengeMaster] Challenge:{challengeTitle} {actionType} unlockItemStackLevel Level:{unlockItemStackLevel.Level} out of range [1,{table.StackCounts.Length}] for ItemGuid:{itemGuid}\n";
                                }
                            }
                            break;
                        }
                        case UnlockBlockGameActionParam unlockBlock:
                        {
                            if (unlockBlock.UnlockBlockGuids == null) break;
                            foreach (var blockGuid in unlockBlock.UnlockBlockGuids)
                            {
                                var blockId = MasterHolder.BlockMaster.GetBlockIdOrNull(blockGuid);
                                if (blockId == null)
                                {
                                    logs += $"[ChallengeMaster] Challenge:{challengeTitle} {actionType} has invalid UnlockBlockGuid:{blockGuid}\n";
                                }
                            }
                            break;
                        }
                        case UnlockTrainCarGameActionParam unlockTrainCar:
                        {
                            if (unlockTrainCar.UnlockTrainCarGuids == null) break;
                            foreach (var trainCarGuid in unlockTrainCar.UnlockTrainCarGuids)
                            {
                                if (!MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(trainCarGuid, out _))
                                {
                                    logs += $"[ChallengeMaster] Challenge:{challengeTitle} {actionType} has invalid UnlockTrainCarGuid:{trainCarGuid}\n";
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
