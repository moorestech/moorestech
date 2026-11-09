using System;
using System.Globalization;
using Client.Localization;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Parts.Rows
{
    /// <summary>
    /// 電力率の文。不足の赤は呼び手が許したときだけ（Web PowerRateText の写し）
    /// Power-rate text, red when lacking only if the caller allows it (port of Web PowerRateText)
    /// </summary>
    public sealed class PowerRateText : VisualElement
    {
        private readonly LackHighlightText _text;

        public PowerRateText(string testid)
        {
            _text = new LackHighlightText(testid, LackHighlightTextSize.Sm);
            Add(_text);
        }

        public void Render(double currentPower, double requestPower, bool highlightShortage)
        {
            // 百分率は JS の Math.round と同じく 0.5 を切り上げる（C# の既定は偶数丸め）
            // The percentage rounds half up like JS Math.round (C# defaults to banker's rounding)
            var rate = BlockDetailMath.ComputePowerRate(currentPower, requestPower);
            var percent = (long)Math.Floor(rate * 100 + 0.5);
            var text = new UiText(LocalizationKeys.Ui.BlockInventory.PowerRateSummary, new[]
            {
                new LocalizationTextParam("rate", percent.ToString(CultureInfo.InvariantCulture)),
                new LocalizationTextParam("current", currentPower.ToString(CultureInfo.InvariantCulture)),
                new LocalizationTextParam("required", requestPower.ToString(CultureInfo.InvariantCulture)),
            });
            _text.Render(text, highlightShortage && rate < 1);
        }
    }
}
