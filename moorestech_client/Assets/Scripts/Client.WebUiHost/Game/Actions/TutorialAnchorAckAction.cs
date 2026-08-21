using Client.Game.InGame.Tutorial;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    public class TutorialAnchorAckAction : IActionHandler
    {
        public string ActionType => "tutorial.anchor_ack";

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            var sessionId = payload?.Value<string>("tutorialSessionId");
            var revision = payload?.Value<int>("revision") ?? -1;
            var store = TutorialPresentationStateStore.Instance;
            if (!store.HasSessionId(sessionId))
                return UniTask.FromResult(ActionResult.Fail("stale_session"));
            if (revision != store.GetCurrent().Revision)
                return UniTask.FromResult(ActionResult.Fail("stale_revision"));
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}
