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

        // レシピ秒の文。数値をそのまま差す
        // Recipe seconds text; seconds are interpolated as the plain number like Web, same format on rows and header
        public static UiText Duration(double seconds)
        {
            return new UiText(LocalizationKeys.Ui.BlockInventory.RecipeDuration, new[] { new LocalizationTextParam("seconds", seconds.ToString(CultureInfo.InvariantCulture)) });
        }

        // 選択行の名前。複数なら他 N 件
        // Selection row name; several products add "and N more" so the name matches the icon row
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
