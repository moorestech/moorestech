using System.Globalization;
using Client.Localization;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.Screens.ModeHud.Parts
{
    // 「Height: {height}」の文を作る唯一の場所（配置 HUD と削除箱の 2 か所が使う）。Web の {height} と同じく符号付きの整数
    // The single place that builds "Height: {height}" (used by the placement HUD and the delete box); a signed integer like Web's {height}
    public static class PlacementHeightText
    {
        public static UiText Create(int height)
        {
            return new UiText(LocalizationKeys.Ui.ModeHud.PlacementHeight, new[] { new LocalizationTextParam("height", height.ToString(CultureInfo.InvariantCulture)) });
        }
    }
}
