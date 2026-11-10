using System;
using System.Globalization;
using Client.Localization;
using Client.Ui.Content;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 機械のレシピの名前（Web useRecipeDisplayName・useRecipeRowName）。名前の解決に Content が要るので画面側に置く
    /// Machine recipe names (Web useRecipeDisplayName, useRecipeRowName); kept on the screen side because names resolve through Content
    /// </summary>
    public static class MachineRecipeNames
    {
        // 代表の名前だけ（ヘッダ用。「他 N 件」は付けない）
        // The representative's name only (for the header; no "and N more")
        public static UiText Representative(RecipeDisplaySubject subject, GameContentPresentation content)
        {
            return subject switch
            {
                ItemRecipeDisplaySubject item => content.ItemName(item.ItemId),
                FluidRecipeDisplaySubject fluid => content.Fluid(fluid.FluidId).Label,
                _ => throw new ArgumentOutOfRangeException(nameof(subject), subject, "RecipeDisplaySubject has only the item and fluid kinds"),
            };
        }

        // 選択リストの行の名前。生産物が複数なら他 N 件を添えてアイコンの並びと食い違わないようにする
        // The selection row name; several products add "and N more" so the name matches the icon row
        public static UiText Row(MachineRecipeSelectionRow row, GameContentPresentation content)
        {
            var name = Representative(row.Subject, content);
            var otherCount = MachineRecipeSelectionLogic.OtherOutputCount(row.Recipe);
            if (otherCount <= 0) return name;
            return new UiText(LocalizationKeys.Ui.BlockInventory.RecipeNameWithOthers, new[]
            {
                new LocalizationTextParam("name", name.Resolve()),
                new LocalizationTextParam("count", otherCount.ToString(CultureInfo.InvariantCulture)),
            });
        }
    }
}
