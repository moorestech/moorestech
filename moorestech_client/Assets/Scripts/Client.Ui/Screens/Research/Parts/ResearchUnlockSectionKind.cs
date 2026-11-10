using System;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.Screens.Research.Parts
{
    public enum ResearchUnlockSectionKind
    {
        Blocks,
        MachineRecipes,
        Items,
        RewardItems,
        Others,
    }

    /// <summary>
    /// 節ごとの見出し・testid・枠の列の有無（種類→節は ResearchUnlockEntryElements.AddTo が返す）
    /// Per-section label, testid and slots-row flag (kind-to-section is returned by ResearchUnlockEntryElements.AddTo)
    /// </summary>
    public static class ResearchUnlockSectionKinds
    {
        public static LocalizationKey LabelKey(ResearchUnlockSectionKind kind)
        {
            return kind switch
            {
                ResearchUnlockSectionKind.Blocks => LocalizationKeys.Ui.Research.UnlockBlocksLabel,
                ResearchUnlockSectionKind.MachineRecipes => LocalizationKeys.Ui.Research.UnlockMachineRecipesLabel,
                ResearchUnlockSectionKind.Items => LocalizationKeys.Ui.Research.UnlockItemsLabel,
                ResearchUnlockSectionKind.RewardItems => LocalizationKeys.Ui.Research.RewardItemsLabel,
                ResearchUnlockSectionKind.Others => LocalizationKeys.Ui.Research.UnlockOthersLabel,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
            };
        }

        public static string Testid(ResearchUnlockSectionKind kind)
        {
            return kind switch
            {
                ResearchUnlockSectionKind.Blocks => "research-unlock-blocks",
                ResearchUnlockSectionKind.MachineRecipes => "research-unlock-machine-recipes",
                ResearchUnlockSectionKind.Items => "research-unlock-items",
                ResearchUnlockSectionKind.RewardItems => "research-reward-items",
                ResearchUnlockSectionKind.Others => "research-unlock-others",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
            };
        }

        // その他の節だけは枠の列で包まず文の行を縦に並べる
        // Only the others section skips the slots row and stacks text rows
        public static bool WrapsInSlotsRow(ResearchUnlockSectionKind kind)
        {
            return kind != ResearchUnlockSectionKind.Others;
        }
    }
}
