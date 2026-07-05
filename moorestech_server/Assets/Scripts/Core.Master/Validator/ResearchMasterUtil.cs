using System;
using System.Collections.Generic;
using Mooresmaster.Model.GameActionModule;
using Mooresmaster.Model.ResearchModule;

namespace Core.Master.Validator
{
    public static class ResearchMasterUtil
    {
        public static bool Validate(Research research, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += ItemValidation();
            errorLogs += PrevResearchValidation();
            errorLogs += GameActionValidation();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string ItemValidation()
            {
                var logs = "";
                foreach (var researchNode in research.Data)
                {
                    foreach (var consumeItem in researchNode.ConsumeItems)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(consumeItem.ItemGuid);
                        if (itemId == null)
                        {
                            logs += $"[ResearchMaster] Research:{researchNode.ResearchNodeName} has invalid ConsumeItem.ItemGuid:{consumeItem.ItemGuid}\n";
                        }
                    }
                }

                return logs;
            }

            string PrevResearchValidation()
            {
                var logs = "";
                foreach (var researchNode in research.Data)
                {
                    foreach (var prevGuid in researchNode.PrevResearchNodeGuids)
                    {
                        if (!ExistsResearchGuid(prevGuid))
                        {
                            logs += $"[ResearchMaster] Research:{researchNode.ResearchNodeName} has invalid PrevResearchNodeGuid:{prevGuid}\n";
                        }
                    }
                }

                return logs;
            }

            bool ExistsResearchGuid(Guid researchGuid)
            {
                return Array.Exists(research.Data, r => r.ResearchNodeGuid == researchGuid);
            }

            string GameActionValidation()
            {
                var logs = "";
                foreach (var researchNode in research.Data)
                {
                    logs += ValidateGameActions(researchNode.ClearedActions.items, researchNode.ResearchNodeName);
                }

                return logs;
            }

            string ValidateGameActions(GameActionElement[] actions, string researchName)
            {
                if (actions == null) return "";

                var localErrors = "";
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
                                    localErrors += $"[ResearchMaster] Research:{researchName} has invalid ClearedAction.UnlockRecipeGuid:{recipeGuid}\n";
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
                                    localErrors += $"[ResearchMaster] Research:{researchName} has invalid ClearedAction.UnlockItemGuid:{itemGuid}\n";
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
                                    localErrors += $"[ResearchMaster] Research:{researchName} has invalid ClearedAction.UnlockMachineRecipeGuid:{machineRecipeGuid}\n";
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
                                    localErrors += $"[ResearchMaster] Research:{researchName} has invalid ClearedAction.RewardItem.ItemGuid:{rewardItem.ItemGuid}\n";
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
                                localErrors += $"[ResearchMaster] Research:{researchName} has invalid (null) ClearedAction.TargetItemGuids\n";
                                break;
                            }
                            foreach (var itemGuid in unlockItemStackLevel.TargetItemGuids)
                            {
                                // 対象アイテムの実在を検証
                                // Validate that the target item exists
                                var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(itemGuid);
                                if (itemId == null)
                                {
                                    localErrors += $"[ResearchMaster] Research:{researchName} has invalid ClearedAction.TargetItemGuid:{itemGuid}\n";
                                    continue;
                                }

                                // 解放レベルが1以上かつスタックテーブル長を超えていないか検証
                                // Validate that the unlock level is at least 1 and does not exceed the stack table length
                                var element = MasterHolder.ItemMaster.GetItemMaster(itemId.Value);
                                var table = MasterHolder.ItemMaster.GetStackLevelTable(element.StackLevelTableGuid);
                                if (unlockItemStackLevel.Level < 1 || table.StackCounts.Length < unlockItemStackLevel.Level)
                                {
                                    localErrors += $"[ResearchMaster] Research:{researchName} unlockItemStackLevel Level:{unlockItemStackLevel.Level} out of range [1,{table.StackCounts.Length}] for ItemGuid:{itemGuid}\n";
                                }
                            }
                            break;
                        }
                    }
                }
                return localErrors;
            }

            #endregion
        }

        public static void Initialize(
            Research research,
            out Dictionary<Guid, ResearchNodeMasterElement> researchElements)
        {
            // リサーチGUIDからリサーチ要素への辞書を構築
            // Build dictionary from research GUID to research element
            researchElements = new Dictionary<Guid, ResearchNodeMasterElement>();
            foreach (var element in research.Data)
            {
                researchElements[element.ResearchNodeGuid] = element;
            }
        }
    }
}
