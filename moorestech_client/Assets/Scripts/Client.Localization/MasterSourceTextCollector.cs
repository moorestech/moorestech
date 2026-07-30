using System.Collections.Generic;
using Core.Master;

namespace Client.Localization
{
    public static class MasterSourceTextCollector
    {
        public static Dictionary<string, string> Collect()
        {
            var sourceTexts = new Dictionary<string, string>();

            // アイテムの安定Guidから原文フォールバックを構築する
            // Build source fallbacks from stable item GUIDs
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

            // 必須Guidから全キャラクターの話者名原文を収集する
            // Collect every character speaker source from its required GUID
            foreach (var characterMaster in MasterHolder.CharacterMaster.Characters.Data)
            {
                sourceTexts[ContentLocalizationKeys.CharacterName(characterMaster.CharacterGuid)] =
                    characterMaster.DisplayName;
            }

            // 研究ノードの名前と説明を同じGuidから収集する
            // Collect research names and descriptions from the same GUID
            foreach (var researchMaster in MasterHolder.ResearchMaster.GetAllResearches())
            {
                sourceTexts[ContentLocalizationKeys.ResearchNodeName(researchMaster.ResearchNodeGuid)] =
                    researchMaster.ResearchNodeName;
                sourceTexts[ContentLocalizationKeys.ResearchNodeDescription(researchMaster.ResearchNodeGuid)] =
                    researchMaster.ResearchNodeDescription;
            }

            // 全カテゴリと配下チャレンジの原文を正本として収集する
            // Collect every category and nested challenge as canonical source text
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
