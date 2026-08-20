using System;
using System.Collections.Generic;
using Core.Master;
using Mooresmaster.Localization.Generated;
using Mooresmaster.Model.ChallengesModule;

namespace Client.Game.Localization
{
    /// <summary>
    ///     Master正本からローカライズ原文を集めてローカライズ基盤へプッシュするためのGame層収集器
    ///     Game-layer collector that gathers canonical Master sources for pushing into the localization foundation
    /// </summary>
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

                    // tutorial文言もGuidで収集（文言フィールドの無い種別はnullを返しスキップ）
                    // Collect tutorial texts by GUID (types with no text field return null and are skipped)
                    foreach (var tutorial in challengeMaster.Tutorials)
                    {
                        var displayText = GetTutorialDisplayText(tutorial);
                        if (displayText == null) continue;
                        sourceTexts[ContentLocalizationKeys.ChallengeTutorialText(tutorial.TutorialGuid).Key] =
                            displayText;
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

            // 車両名を必須Guidから収集
            // Collect train car names from required GUIDs
            foreach (var trainCarMaster in MasterHolder.TrainUnitMaster.Train.TrainCars)
            {
                sourceTexts[ContentLocalizationKeys.TrainCarName(trainCarMaster.TrainCarGuid).Key] =
                    trainCarMaster.Name;
            }

            return sourceTexts;

            #region Internal

            string GetTutorialDisplayText(TutorialsElement tutorial)
            {
                // tutorialTypeごとの表示文言フィールドを一元定義
                // Define the display-text field per tutorial type in one place
                return tutorial.TutorialParam switch
                {
                    MapObjectPinTutorialParam mapObjectPin => mapObjectPin.PinText,
                    VeinPinTutorialParam veinPin => veinPin.PinText,
                    KeyControlTutorialParam keyControl => keyControl.ControlText,
                    UiHighLightTutorialParam uiHighLight => uiHighLight.HighLightText,
                    ItemViewHighLightTutorialParam itemViewHighLight => itemViewHighLight.HighLightText,
                    BlockPlacePreviewTutorialParam blockPlacePreview => blockPlacePreview.Message,
                    // uiDragGuideはfrom/toのanchorIdのみで表示文言フィールドを持たない
                    // uiDragGuide has only from/to anchorIds and no display-text field
                    UiDragGuideTutorialParam => null,
                    _ => throw new InvalidOperationException(
                        $"Unknown tutorial type: {tutorial.TutorialType}"),
                };
            }

            #endregion
        }
    }
}
