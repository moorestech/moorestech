using Client.Localization;
using Client.WebUiHost.Boot;
using Cysharp.Threading.Tasks;
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

            // 選択可否の判定はLocalize側に集約し、結果を失敗契約へ写す
            // Delegate the selectability judgement to Localize and map the result to the failure contract
            return UniTask.FromResult(Localize.TrySetLanguage(locale)
                ? ActionResult.Success()
                : ActionResult.Fail("unknown_locale"));
        }
    }
}
