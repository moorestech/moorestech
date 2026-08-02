using System.Collections.Generic;
using Core.Master;
using Mooresmaster.Localization.Generated;
using Mooresmaster.Model.ChallengesModule;

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
                sourceTexts[ContentLocalizationKeys.ItemName(itemMaster.ItemGuid).Key] = itemMaster.Name;
            }

            // ブロックも同じ導出規約で原文を収集する
            // Collect block sources with the same derived-key convention
            foreach (var blockMaster in MasterHolder.BlockMaster.Blocks.Data)
            {
                sourceTexts[ContentLocalizationKeys.BlockName(blockMaster.BlockGuid).Key] = blockMaster.Name;
            }

            // 分類名を必須Guidから収集
            // Collect classification names from required GUIDs
            foreach (var categoryMaster in MasterHolder.BuildMenuCategoryMaster.Categories)
            {
                sourceTexts[ContentLocalizationKeys.BuildMenuCategoryName(categoryMaster.CategoryGuid).Key] =
                    categoryMaster.Name;
                foreach (var subCategoryMaster in categoryMaster.SubCategories)
                {
                    sourceTexts[ContentLocalizationKeys.BuildMenuSubCategoryName(
                        subCategoryMaster.SubCategoryGuid).Key] = subCategoryMaster.Name;
                }
            }

            // 話者名原文を必須Guidから収集
            // Collect speaker sources from required GUIDs
            foreach (var characterMaster in MasterHolder.CharacterMaster.Characters.Data)
            {
                sourceTexts[ContentLocalizationKeys.CharacterName(characterMaster.CharacterGuid).Key] =
                    characterMaster.DisplayName;
            }

            // 研究名と説明を同じGuidから収集
            // Collect research names and descriptions from one GUID
            foreach (var researchMaster in MasterHolder.ResearchMaster.GetAllResearches())
            {
                sourceTexts[ContentLocalizationKeys.ResearchName(researchMaster.ResearchNodeGuid).Key] =
                    researchMaster.ResearchNodeName;
                sourceTexts[ContentLocalizationKeys.ResearchDescription(researchMaster.ResearchNodeGuid).Key] =
                    researchMaster.ResearchNodeDescription;
            }

            // 全カテゴリ配下を正本として収集
            // Collect every category subtree as canonical source
            foreach (var categoryMaster in MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements)
            {
                sourceTexts[ContentLocalizationKeys.ChallengeCategoryName(categoryMaster.CategoryGuid).Key] =
                    categoryMaster.CategoryName;
                sourceTexts[ContentLocalizationKeys.ChallengeCategoryDescription(categoryMaster.CategoryGuid).Key] =
                    categoryMaster.CategoryDescription;
                foreach (var challengeMaster in categoryMaster.Challenges)
                {
                    sourceTexts[ContentLocalizationKeys.ChallengeTitle(challengeMaster.ChallengeGuid).Key] =
                        challengeMaster.Title;
                    sourceTexts[ContentLocalizationKeys.ChallengeSummary(challengeMaster.ChallengeGuid).Key] =
                        challengeMaster.Summary;

                    // チュートリアル表示文言もtutorialGuidで収集
                    // Collect tutorial display texts by tutorial GUID
                    foreach (var tutorial in challengeMaster.Tutorials)
                    {
                        sourceTexts[ContentLocalizationKeys.ChallengeTutorialText(tutorial.TutorialGuid).Key] =
                            GetTutorialDisplayText(tutorial);
                    }
                }
            }

            // 接続ツール名を必須Guidから収集
            // Collect connect tool names from required GUIDs
            foreach (var connectToolMaster in MasterHolder.ConnectToolMaster.All)
            {
                sourceTexts[ContentLocalizationKeys.ConnectToolName(connectToolMaster.ConnectToolGuid).Key] =
                    connectToolMaster.Name;
            }

            // 流体名も同じ導出規約で原文を収集する
            // Collect fluid sources with the same derived-key convention
            foreach (var fluidMaster in MasterHolder.FluidMaster.Fluids.Data)
            {
                sourceTexts[ContentLocalizationKeys.FluidName(fluidMaster.FluidGuid).Key] = fluidMaster.Name;
            }

            return sourceTexts;
        }

        public static string GetTutorialDisplayText(TutorialsElement tutorial)
        {
            // tutorialTypeごとの表示文言フィールドを一元定義
            // Define the display-text field per tutorial type in one place
            return tutorial.TutorialParam switch
            {
                MapObjectPinTutorialParam mapObjectPin => mapObjectPin.PinText,
                KeyControlTutorialParam keyControl => keyControl.ControlText,
                UiHighLightTutorialParam uiHighLight => uiHighLight.HighLightText,
                ItemViewHighLightTutorialParam itemViewHighLight => itemViewHighLight.HighLightText,
                BlockPlacePreviewTutorialParam blockPlacePreview => blockPlacePreview.Message,
                _ => throw new System.InvalidOperationException(
                    $"Unknown tutorial type: {tutorial.TutorialType}"),
            };
        }
    }
}
