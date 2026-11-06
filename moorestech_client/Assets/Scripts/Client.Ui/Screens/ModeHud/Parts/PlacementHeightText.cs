using System.Globalization;
using Client.Localization;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.Screens.ModeHud.Parts
{
    // 「Height: {height}」文を作る唯一の場所
    // The single place that builds "Height: {height}"
    public static class PlacementHeightText
    {
        public static UiText Create(int height)
        {
            return new UiText(LocalizationKeys.Ui.ModeHud.PlacementHeight, new[] { new LocalizationTextParam("height", height.ToString(CultureInfo.InvariantCulture)) });
        }
    }
}
