using Client.Game.InGame.UI.ContextMenu;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    public class ContextMenuSelectActionHandler : IActionHandler
    {
        private readonly ContextMenuView _view;
        public string ActionType => "context_menu.select";
        public ContextMenuSelectActionHandler(ContextMenuView view) { _view = view; }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload?["id"]?.Type != JTokenType.String) return UniTask.FromResult(ActionResult.Fail("invalid_id"));
            return UniTask.FromResult(_view.TrySelect((string)payload["id"])
                ? ActionResult.Success()
                : ActionResult.Fail("invalid_id"));
        }
    }

    public class ContextMenuCloseActionHandler : IActionHandler
    {
        private readonly ContextMenuView _view;
        public string ActionType => "context_menu.close";
        public ContextMenuCloseActionHandler(ContextMenuView view) { _view = view; }
        public UniTask<ActionResult> ExecuteAsync(JObject payload) { _view.Hide(); return UniTask.FromResult(ActionResult.Success()); }
    }
}
