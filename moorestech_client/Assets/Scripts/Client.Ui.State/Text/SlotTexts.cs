using Client.Localization;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.Text
{
    /// <summary>
    /// スロット族の引数付きの文。Components が Client.Localization を参照せずに済むようここで組む
    /// Parameterised sentences for the slot family, built here so Components never reference Client.Localization
    /// </summary>
    public static class SlotTexts
    {
        public static UiText IconIdFallback(string idText)
        {
            return new UiText(LocalizationKeys.Ui.Common.IconIdFallback, new[] { new LocalizationTextParam("id", idText) });
        }

        // 所持/必要の数は量バッジと同じ書式を通す（Web の formatSlotAmount）
        // Owned/required go through the same formatting as the amount badge (Web formatSlotAmount)
        public static UiText ItemCountSummary(double ownedCount, double requiredCount)
        {
            return new UiText(LocalizationKeys.Ui.Recipe.ItemCountSummary, new[]
            {
                new LocalizationTextParam("ownedCount", SlotAmountFormat.Format(ownedCount)),
                new LocalizationTextParam("requiredCount", SlotAmountFormat.Format(requiredCount)),
            });
        }
    }
}
