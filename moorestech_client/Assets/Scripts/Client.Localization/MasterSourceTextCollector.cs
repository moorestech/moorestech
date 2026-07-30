using System.Collections.Generic;
using Core.Master;

namespace Client.Localization
{
    public static class MasterSourceTextCollector
    {
        public static Dictionary<string, string> Collect()
        {
            var sourceTexts = new Dictionary<string, string>();

            // 安定Guidで原文fallback構築
            // Build source fallbacks from stable GUIDs
            foreach (var itemId in MasterHolder.ItemMaster.GetItemAllIds())
            {
                var itemMaster = MasterHolder.ItemMaster.GetItemMaster(itemId);
                sourceTexts[ContentLocalizationKeys.ItemName(itemMaster.ItemGuid)] = itemMaster.Name;
            }

            // ブロックも同じ導出規約で原文を収集する
            // Collect block sources with the same derived-key convention
            foreach (var blockMaster in MasterHolder.BlockMaster.Blocks.Data)
            {
                sourceTexts[ContentLocalizationKeys.BlockName(blockMaster.BlockGuid)] = blockMaster.Name;
            }

            // 分類名を必須Guidから収集
            // Collect classification names from required GUIDs
            foreach (var categoryMaster in MasterHolder.BuildMenuCategoryMaster.Categories)
            {
                sourceTexts[ContentLocalizationKeys.BuildMenuCategoryName(categoryMaster.CategoryGuid)] =
                    categoryMaster.Name;
                foreach (var subCategoryMaster in categoryMaster.SubCategories)
                {
                    sourceTexts[ContentLocalizationKeys.BuildMenuSubCategoryName(
                        subCategoryMaster.SubCategoryGuid)] = subCategoryMaster.Name;
                }
            }

            // 話者名原文を必須Guidから収集
            // Collect speaker sources from required GUIDs
            foreach (var characterMaster in MasterHolder.CharacterMaster.Characters.Data)
            {
                sourceTexts[ContentLocalizationKeys.CharacterName(characterMaster.CharacterGuid)] =
                    characterMaster.DisplayName;
            }

            // 研究名と説明を同じGuidから収集
            // Collect research names and descriptions from one GUID
            foreach (var researchMaster in MasterHolder.ResearchMaster.GetAllResearches())
            {
                sourceTexts[ContentLocalizationKeys.ResearchNodeName(researchMaster.ResearchNodeGuid)] =
                    researchMaster.ResearchNodeName;
                sourceTexts[ContentLocalizationKeys.ResearchNodeDescription(researchMaster.ResearchNodeGuid)] =
                    researchMaster.ResearchNodeDescription;
            }

            // 全カテゴリ配下を正本として収集
            // Collect every category subtree as canonical source
            foreach (var categoryMaster in MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements)
            {
                sourceTexts[ContentLocalizationKeys.ChallengeCategoryName(categoryMaster.CategoryGuid)] =
                    categoryMaster.CategoryName;
                sourceTexts[ContentLocalizationKeys.ChallengeCategoryDescription(categoryMaster.CategoryGuid)] =
                    categoryMaster.CategoryDescription;
                foreach (var challengeMaster in categoryMaster.Challenges)
                {
                    sourceTexts[ContentLocalizationKeys.ChallengeTitle(challengeMaster.ChallengeGuid)] =
                        challengeMaster.Title;
                    sourceTexts[ContentLocalizationKeys.ChallengeSummary(challengeMaster.ChallengeGuid)] =
                        challengeMaster.Summary;
                }
            }

            return sourceTexts;
        }
    }
}
