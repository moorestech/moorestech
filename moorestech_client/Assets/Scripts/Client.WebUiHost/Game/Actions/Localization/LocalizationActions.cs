using Client.Ui.State.Commands;
using Client.Ui.State.Settings;
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
        public static void Register(WebSocketHub hub, ISettingsCommands commands)
        {
            hub.RegisterAction(new SetLocaleActionHandler(commands));
        }
    }

    /// <summary>
    /// Webからの言語切替を既存Localizeライフサイクルへ接続する
    /// Connects Web locale changes to the existing Localize lifecycle
    /// </summary>
    public class SetLocaleActionHandler : IActionHandler
    {
        private readonly ISettingsCommands _commands;
        public string ActionType => "localization.setLocale";

        public SetLocaleActionHandler(ISettingsCommands commands)
        {
            _commands = commands;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            var locale = payload?["locale"]?.ToString();

            // 選択可否の判定は型付きコマンドに集約し、結果を失敗契約へ写す
            // Delegate the selectability judgement to the typed command and map the result to the failure contract
            return UniTask.FromResult(_commands.SetLanguage(locale) switch
            {
                UiCommandAccepted => ActionResult.Success(),
                UiCommandRejected rejected => ActionResult.Fail(rejected.ToWireCode()),
                _ => throw new System.ArgumentOutOfRangeException(),
            });
        }
    }
}
