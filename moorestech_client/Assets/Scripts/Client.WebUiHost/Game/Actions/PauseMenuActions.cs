using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Ui.State.PauseMenu;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.WebUiHost.Game.Actions
{
    public class PauseMenuSaveActionHandler : IActionHandler
    {
        private readonly IPauseMenuCommands _commands;
        public string ActionType => "pause_menu.save";

        public PauseMenuSaveActionHandler(IPauseMenuCommands commands)
        {
            _commands = commands;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            _commands.Save();
            return UniTask.FromResult(ActionResult.Success());
        }
    }

    public class PauseMenuSaveAndQuitActionHandler : IActionHandler
    {
        private readonly IPauseMenuCommands _commands;
        public string ActionType => "pause_menu.save_and_quit";

        public PauseMenuSaveAndQuitActionHandler(IPauseMenuCommands commands)
        {
            _commands = commands;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            _commands.SaveAndQuit();
            return UniTask.FromResult(ActionResult.Success());
        }
    }

    // Web発の画面遷移要求。画面名を解いて型付きコマンドへ渡す
    // Page-move requests from Web; parses the page name and hands it to the typed command
    public class PauseMenuShowPageActionHandler : IActionHandler
    {
        private readonly IPauseMenuCommands _commands;
        public string ActionType => "pause_menu.show_page";

        public PauseMenuShowPageActionHandler(IPauseMenuCommands commands)
        {
            _commands = commands;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            // 画面名はwebuiの定数が必ず載せる。範囲外は壊れた要求として拒否する
            // The webui constants always send a page name; an out-of-range value is a broken request
            var pageText = payload?["page"]?.ToString() ?? "";
            if (!PauseMenuPageContract.TryParse(pageText, out var page))
            {
                Debug.LogWarning($"ポーズメニューの画面名が不正なため遷移しません page:{pageText}");
                return UniTask.FromResult(ActionResult.Fail("invalid_page"));
            }

            _commands.ShowPage(page);
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}
