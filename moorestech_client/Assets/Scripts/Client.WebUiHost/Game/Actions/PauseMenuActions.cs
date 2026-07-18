using Client.Game.InGame.Presenter.PauseMenu;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    public class PauseMenuSaveActionHandler : IActionHandler
    {
        private readonly SaveButton _saveButton;
        public string ActionType => "pause_menu.save";

        public PauseMenuSaveActionHandler(SaveButton saveButton)
        {
            _saveButton = saveButton;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            _saveButton.Save();
            return UniTask.FromResult(ActionResult.Success());
        }
    }

    public class PauseMenuBackToMainMenuActionHandler : IActionHandler
    {
        private readonly BackToMainMenu _backToMainMenu;
        public string ActionType => "pause_menu.back_to_main_menu";

        public PauseMenuBackToMainMenuActionHandler(BackToMainMenu backToMainMenu)
        {
            _backToMainMenu = backToMainMenu;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            _backToMainMenu.Back();
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}
