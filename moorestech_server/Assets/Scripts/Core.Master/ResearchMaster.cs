using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Loader.ResearchModule;
using Mooresmaster.Model.GameActionModule;
using Mooresmaster.Model.ResearchModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class ResearchMaster : IMasterValidator
    {
        public readonly Research Research;
        public Dictionary<Guid, ResearchNodeMasterElement> ResearchElements { get; private set; }

        public ResearchMaster(JToken jToken)
        {
            Research = ResearchLoader.Load(jToken);
        }

        public bool Validate(out string errorLogs)
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
                foreach (var research in Research.Data)
                {
                    foreach (var consumeItem in research.ConsumeItems)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(consumeItem.ItemGuid);
                        if (itemId == null)
                        {
                            logs += $"[ResearchMaster] Research:{research.ResearchNodeName} has invalid ConsumeItem.ItemGuid:{consumeItem.ItemGuid}\n";
                        }
                    }
                }

                return logs;
            }

            string PrevResearchValidation()
            {
                var logs = "";
                foreach (var research in Research.Data)
                {
                    foreach (var prevGuid in research.PrevResearchNodeGuids)
                    {
                        if (!ExistsResearchGuid(prevGuid))
                        {
                            logs += $"[ResearchMaster] Research:{research.ResearchNodeName} has invalid PrevResearchNodeGuid:{prevGuid}\n";
                        }
                    }
                }

                return logs;
            }

            bool ExistsResearchGuid(Guid researchGuid)
            {
                return Array.Exists(Research.Data, r => r.ResearchNodeGuid == researchGuid);
            }

            string GameActionValidation()
            {
                var logs = "";
                foreach (var research in Research.Data)
                {
                    logs += ValidateGameActions(research.ClearedActions.items, research.ResearchNodeName);
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

        public void Initialize()
        {
            // リサーチGUIDからリサーチ要素への辞書を構築
            // Build dictionary from research GUID to research element
            ResearchElements = new Dictionary<Guid, ResearchNodeMasterElement>();
            foreach (var element in Research.Data)
            {
                ResearchElements[element.ResearchNodeGuid] = element;
            }
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