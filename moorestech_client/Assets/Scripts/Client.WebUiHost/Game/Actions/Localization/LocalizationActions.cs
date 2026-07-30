using Client.Localization;
using Client.WebUiHost.Boot;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// 依存不要のローカライズアクションをHubへ登録する
    /// Registers dependency-free localization actions with the Hub
    /// </summary>
    public static class LocalizationActions
    {
        public static void Register(WebSocketHub hub)
        {
            hub.RegisterAction(new SetLocaleActionHandler());
        }
    }

    /// <summary>
    /// Webからの言語切替を既存Localizeライフサイクルへ接続する
    /// Connects Web locale changes to the existing Localize lifecycle
    /// </summary>
    public class SetLocaleActionHandler : IActionHandler
    {
        public string ActionType => "localization.setLocale";

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            var locale = payload?["locale"]?.ToString();

            // 外部入力を埋め込み言語カタログへ照合してから状態を変更する
            // Validate external input against the embedded catalog before mutating state
            if (!IsSelectableLocale(locale))
                return UniTask.FromResult(ActionResult.Fail("unknown_locale"));

            Localize.SetLanguage(locale);
            return UniTask.FromResult(ActionResult.Success());
        }

        private static bool IsSelectableLocale(string locale)
        {
            if (string.IsNullOrEmpty(locale)) return false;

            foreach (var language in LanguageCatalog.Languages)
            {
                if (language.Code == locale) return true;
            }

            return false;
        }
    }
}
