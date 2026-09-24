using Client.Game.InGame.Presenter.PauseMenu;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.WebUiHost.Game.Topics;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.WebUiHost.Game.Actions
{
    public class PauseMenuSaveActionHandler : IActionHandler
    {
        private readonly GameSaveRequester _saveRequester;
        public string ActionType => "pause_menu.save";

        public PauseMenuSaveActionHandler(GameSaveRequester saveRequester)
        {
            _saveRequester = saveRequester;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            _saveRequester.Save();
            return UniTask.FromResult(ActionResult.Success());
        }
    }

    public class PauseMenuSaveAndQuitActionHandler : IActionHandler
    {
        private readonly SaveAndQuitPresenter _saveAndQuitPresenter;
        public string ActionType => "pause_menu.save_and_quit";

        public PauseMenuSaveAndQuitActionHandler(SaveAndQuitPresenter saveAndQuitPresenter)
        {
            _saveAndQuitPresenter = saveAndQuitPresenter;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            _saveAndQuitPresenter.SaveAndQuit();
            return UniTask.FromResult(ActionResult.Success());
        }
    }

    // Webのボタンからの画面遷移要求。画面の持ち主はPauseMenuStateService
    // Page-move requests from the Web buttons; PauseMenuStateService owns the page
    public class PauseMenuShowPageActionHandler : IActionHandler
    {
        private readonly PauseMenuStateService _pauseMenuStateService;
        public string ActionType => "pause_menu.show_page";

        public PauseMenuShowPageActionHandler(PauseMenuStateService pauseMenuStateService)
        {
            _pauseMenuStateService = pauseMenuStateService;
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

            _pauseMenuStateService.ShowPage(page);
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}
