using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Loader.ResearchModule;
using Mooresmaster.Model.GameActionModule;
using Mooresmaster.Model.ResearchModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class ResearchMaster
    {
        public readonly Research Research;
        public readonly Dictionary<Guid, ResearchNodeMasterElement> ResearchElements;

        public ResearchMaster(JToken jToken)
        {
            Research = ResearchLoader.Load(jToken);
            ResearchElements = new Dictionary<Guid, ResearchNodeMasterElement>();
            foreach (var element in Research.Data)
            {
                ResearchElements[element.ResearchNodeGuid] = element;
            }

            // 外部キーバリデーション
            // Foreign key validation
            ItemValidation();
            PrevResearchValidation();
            GameActionValidation();

            #region Internal

            void ItemValidation()
            {
                var errorLogs = "";
                foreach (var research in Research.Data)
                {
                    foreach (var consumeItem in research.ConsumeItems)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(consumeItem.ItemGuid);
                        if (itemId == null)
                        {
                            errorLogs += $"[ResearchMaster] Research:{research.ResearchNodeName} has invalid ConsumeItem.ItemGuid:{consumeItem.ItemGuid}\n";
                        }
                    }
                }

                if (!string.IsNullOrEmpty(errorLogs))
                {
                    throw new Exception(errorLogs);
                }
            }

            void PrevResearchValidation()
            {
                var errorLogs = "";
                foreach (var research in Research.Data)
                {
                    foreach (var prevGuid in research.PrevResearchNodeGuids)
                    {
                        if (!ResearchElements.ContainsKey(prevGuid))
                        {
                            errorLogs += $"[ResearchMaster] Research:{research.ResearchNodeName} has invalid PrevResearchNodeGuid:{prevGuid}\n";
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
                foreach (var research in Research.Data)
                {
                    errorLogs += ValidateGameActions(research.ClearedActions.items, research.ResearchNodeName);
                }

                if (!string.IsNullOrEmpty(errorLogs))
                {
                    throw new Exception(errorLogs);
                }
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
                                var recipe = MasterHolder.CraftRecipeMaster.GetCraftRecipe(recipeGuid);
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
                    }
                }
                return localErrors;
            }

            #endregion
        }

        public ResearchNodeMasterElement GetResearch(Guid researchGuid)
        {
            return ResearchElements.GetValueOrDefault(researchGuid);
        }

        public List<ResearchNodeMasterElement> GetAllResearches()
        {
            return ResearchElements.Values.ToList();
        }
    }
}