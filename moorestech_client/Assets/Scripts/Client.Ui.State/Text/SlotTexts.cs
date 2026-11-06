using Client.Localization;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.Text
{
    /// <summary>
    /// 引数付きの文を組む（Components が Localization を参照しない為）
    /// Builds parameterised sentences so Components skip Localization
    /// </summary>
    public static class SlotTexts
    {
        public static UiText IconIdFallback(string idText)
        {
            return new UiText(LocalizationKeys.Ui.Common.IconIdFallback, new[] { new LocalizationTextParam("id", idText) });
        }

        // 量バッジと同じ書式を通す（Web の formatSlotAmount）
        // Use the amount badge formatting (Web formatSlotAmount)
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
