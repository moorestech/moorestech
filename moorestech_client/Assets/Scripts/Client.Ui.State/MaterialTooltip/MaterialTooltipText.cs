using System;
using Client.Localization;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.MaterialTooltip
{
    /// <summary>
    /// 素材ツールチップを名乗る辞書キーだけを選べる種類。任意のキーへ所持数の語彙を当てさせない
    /// The kinds that may claim a material tooltip; no arbitrary key gets the owned-count vocabulary
    /// </summary>
    public enum MaterialTooltipKind
    {
        RecipeMaterial,
        ResearchConsumeItem,
        BuildMenuMaterial,
        BuildMenuShortageLine,
    }

    /// <summary>
    /// 素材ツールチップ共通の文（名前・所持数・必要数）。Web materialTooltipText.ts の写し
    /// The shared material-tooltip sentence (name, owned, required); port of the Web materialTooltipText.ts
    /// </summary>
    public static class MaterialTooltipText
    {
        // 数量を語る表示は量バッジと同じ書式を通し、1 つのスロットで表記を割らない
        // Every quantity goes through the badge formatting so one slot never spells numbers two ways
        public static UiText Create(MaterialTooltipKind kind, string itemName, double requiredCount, double ownedCount)
        {
            return new UiText(KeyOf(kind), new[]
            {
                new LocalizationTextParam("itemName", itemName),
                new LocalizationTextParam("ownedCount", SlotAmountFormat.Format(ownedCount)),
                new LocalizationTextParam("requiredCount", SlotAmountFormat.Format(requiredCount)),
            });

            #region Internal

            LocalizationKey KeyOf(MaterialTooltipKind materialKind)
            {
                return materialKind switch
                {
                    MaterialTooltipKind.RecipeMaterial => LocalizationKeys.Ui.Recipe.MaterialTooltip,
                    MaterialTooltipKind.ResearchConsumeItem => LocalizationKeys.Ui.Research.ConsumeItemTooltip,
                    MaterialTooltipKind.BuildMenuMaterial => LocalizationKeys.Ui.BuildMenu.MaterialTooltip,
                    MaterialTooltipKind.BuildMenuShortageLine => LocalizationKeys.Ui.BuildMenu.MaterialShortageLine,
                    _ => throw new ArgumentOutOfRangeException(nameof(materialKind), materialKind, "未知の素材ツールチップの種類"),
                };
            }

            #endregion
        }
    }
}
