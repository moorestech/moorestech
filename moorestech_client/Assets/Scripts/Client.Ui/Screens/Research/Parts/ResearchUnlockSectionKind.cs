using System;
using Client.Ui.State.Research.Logic;
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
    /// 解放物の種類→節の唯一の表
    /// Single table from unlock kind to section
    /// </summary>
    public static class ResearchUnlockSectionKinds
    {
        public static ResearchUnlockSectionKind Of(UnlockEntry entry)
        {
            return entry switch
            {
                BlockUnlockEntry => ResearchUnlockSectionKind.Blocks,
                MachineRecipeOutputUnlockEntry => ResearchUnlockSectionKind.MachineRecipes,
                ItemRecipeViewUnlockEntry => ResearchUnlockSectionKind.Items,
                RewardItemUnlockEntry => ResearchUnlockSectionKind.RewardItems,
                ConnectToolUnlockEntry or TrainCarUnlockEntry or ModuleSlotStackLimitUnlockEntry or LevelInheritanceCraftCapUnlockEntry => ResearchUnlockSectionKind.Others,
                _ => throw new ArgumentOutOfRangeException(nameof(entry), entry.GetType().Name, null),
            };
        }

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
