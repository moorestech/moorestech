using Client.Skit.UI;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    public abstract class SkitActionHandlerBase : IActionHandler
    {
        protected readonly SkitPresentationStateStore Store;
        public abstract string ActionType { get; }

        protected SkitActionHandlerBase(SkitPresentationStateStore store)
        {
            Store = store;
        }

        public abstract UniTask<ActionResult> ExecuteAsync(JObject payload);

        protected static ActionResult Convert(SkitIntentResult result)
        {
            return result.Ok ? ActionResult.Success() : ActionResult.Fail(result.Error);
        }

        protected static bool TryReadBase(JObject payload, out string sessionId, out int revision)
        {
            sessionId = payload?.Value<string>("sessionId");
            revision = payload?.Value<int?>("sceneRevision") ?? -1;
            return sessionId != null && revision >= 0;
        }
    }

    public class SkitAdvanceActionHandler : SkitActionHandlerBase
    {
        public override string ActionType => "skit.advance";
        public SkitAdvanceActionHandler(SkitPresentationStateStore store) : base(store) { }

        public override UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (!TryReadBase(payload, out var sessionId, out var revision))
                return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            return UniTask.FromResult(Convert(Store.TryAdvance(sessionId, revision)));
        }
    }

    public class SkitSelectActionHandler : SkitActionHandlerBase
    {
        public override string ActionType => "skit.select";
        public SkitSelectActionHandler(SkitPresentationStateStore store) : base(store) { }

        public override UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (!TryReadBase(payload, out var sessionId, out var revision))
                return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            return UniTask.FromResult(Convert(Store.TrySelect(
                sessionId, revision, payload.Value<string>("choiceId"))));
        }
    }

    public class SkitSetAutoActionHandler : SkitActionHandlerBase
    {
        public override string ActionType => "skit.set_auto";
        public SkitSetAutoActionHandler(SkitPresentationStateStore store) : base(store) { }

        public override UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (!TryReadBase(payload, out var sessionId, out var revision) ||
                payload?["enabled"]?.Type != JTokenType.Boolean)
                return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            return UniTask.FromResult(Convert(Store.TrySetAuto(
                sessionId, revision, payload.Value<bool>("enabled"))));
        }
    }

    public class SkitSkipActionHandler : SkitActionHandlerBase
    {
        public override string ActionType => "skit.skip";
        public SkitSkipActionHandler(SkitPresentationStateStore store) : base(store) { }

        public override UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (!TryReadBase(payload, out var sessionId, out var revision))
                return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            return UniTask.FromResult(Convert(Store.TrySkip(sessionId, revision)));
        }
    }

    public class SkitSetUiHiddenActionHandler : SkitActionHandlerBase
    {
        public override string ActionType => "skit.set_ui_hidden";
        public SkitSetUiHiddenActionHandler(SkitPresentationStateStore store) : base(store) { }

        public override UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (!TryReadBase(payload, out var sessionId, out var revision) ||
                payload?["hidden"]?.Type != JTokenType.Boolean)
                return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            return UniTask.FromResult(Convert(Store.TrySetUiHidden(
                sessionId, revision, payload.Value<bool>("hidden"))));
        }
    }
}
