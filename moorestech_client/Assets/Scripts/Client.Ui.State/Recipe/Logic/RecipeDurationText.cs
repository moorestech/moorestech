using Client.Localization;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.Recipe.Logic
{
    /// <summary>
    /// 矢印の上の秒数（Web t(ui.recipe.duration, {seconds}) の写し。数は JS の既定の文字化と同じ最短表記）
    /// Seconds above the arrow (port of the Web t(ui.recipe.duration, {seconds}); the number uses the shortest round-trip form like JS)
    /// </summary>
    public static class RecipeDurationText
    {
        public static UiText Create(double seconds)
        {
            return new UiText(LocalizationKeys.Ui.Recipe.Duration, new[] { new LocalizationTextParam("seconds", UiNumberFormat.JsString(seconds)) });
        }
    }
}
