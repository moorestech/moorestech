using System.Globalization;
using Client.Localization;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Parts.Rows
{
    /// <summary>
    /// アイコンと分間レートの組。採掘機とポンプが共有する（Web PerMinuteRateRow の写し。Group gap 4）
    /// An icon paired with its per-minute rate, shared by the miner and the pump (port of Web PerMinuteRateRow; Group gap 4)
    /// </summary>
    public sealed class PerMinuteRateRow : VisualElement
    {
        private readonly LocalizedLabel _text;

        public PerMinuteRateRow(VisualElement leading)
        {
            var group = new UiGroup(UiGap.Px4);
            _text = new LocalizedLabel();
            _text.AddToClassList(UiTypographyClass.TextXs);
            _text.AddToClassList("per-minute-rate-row__text");
            group.AddItem(leading);
            group.AddItem(_text);
            Add(group);
        }

        public void Render(double amountPerMinute)
        {
            // 小数 1 桁（Web の toFixed(1)）
            // One decimal place (the Web toFixed(1))
            var perMinute = amountPerMinute.ToString("F1", CultureInfo.InvariantCulture);
            _text.SetText(new UiText(LocalizationKeys.Ui.BlockInventory.ItemsPerMinute, new[] { new LocalizationTextParam("itemsPerMinute", perMinute) }));
        }
    }
}
