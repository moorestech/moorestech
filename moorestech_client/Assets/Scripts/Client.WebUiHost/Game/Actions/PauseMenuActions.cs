using Client.Game.InGame.Presenter.PauseMenu;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

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
}
