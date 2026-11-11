using Client.Ui.State.Commands;
using Client.Ui.State.Skit;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// skit.* action の payload を読んで ISkitCommands へ渡す。失敗の写しは SkitCommands 1 本で、GameUi のトーストと同じ結果を wire へ返す（S2-skit D-54）
    /// Reads skit.* payloads and hands them to ISkitCommands; SkitCommands is the only failure mapping, so the wire gets the same result as the GameUi toast (S2-skit D-54)
    /// </summary>
    public abstract class SkitActionHandlerBase : IActionHandler
    {
        protected readonly ISkitCommands Commands;
        public abstract string ActionType { get; }

        protected SkitActionHandlerBase(ISkitCommands commands)
        {
            Commands = commands;
        }

        public abstract UniTask<ActionResult> ExecuteAsync(JObject payload);

        protected static UniTask<ActionResult> Reply(UiCommandResult result)
        {
            return UniTask.FromResult(ActionResult.FromCommandResult(result));
        }

        protected static UniTask<ActionResult> InvalidPayload()
        {
            return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
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
        public SkitAdvanceActionHandler(ISkitCommands commands) : base(commands) { }

        public override UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (!TryReadBase(payload, out var sessionId, out var revision)) return InvalidPayload();
            return Reply(Commands.Advance(sessionId, revision));
        }
    }

    public class SkitSelectActionHandler : SkitActionHandlerBase
    {
        public override string ActionType => "skit.select";
        public SkitSelectActionHandler(ISkitCommands commands) : base(commands) { }

        public override UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (!TryReadBase(payload, out var sessionId, out var revision)) return InvalidPayload();
            return Reply(Commands.Select(sessionId, revision, payload.Value<string>("choiceId")));
        }
    }

    public class SkitSetAutoActionHandler : SkitActionHandlerBase
    {
        public override string ActionType => "skit.set_auto";
        public SkitSetAutoActionHandler(ISkitCommands commands) : base(commands) { }

        public override UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (!TryReadBase(payload, out var sessionId, out var revision) ||
                payload?["enabled"]?.Type != JTokenType.Boolean)
                return InvalidPayload();
            return Reply(Commands.SetAuto(sessionId, revision, payload.Value<bool>("enabled")));
        }
    }

    public class SkitSkipActionHandler : SkitActionHandlerBase
    {
        public override string ActionType => "skit.skip";
        public SkitSkipActionHandler(ISkitCommands commands) : base(commands) { }

        public override UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (!TryReadBase(payload, out var sessionId, out var revision)) return InvalidPayload();
            return Reply(Commands.Skip(sessionId, revision));
        }
    }

    public class SkitSetUiHiddenActionHandler : SkitActionHandlerBase
    {
        public override string ActionType => "skit.set_ui_hidden";
        public SkitSetUiHiddenActionHandler(ISkitCommands commands) : base(commands) { }

        public override UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (!TryReadBase(payload, out var sessionId, out var revision) ||
                payload?["hidden"]?.Type != JTokenType.Boolean)
                return InvalidPayload();
            return Reply(Commands.SetUiHidden(sessionId, revision, payload.Value<bool>("hidden")));
        }
    }
}
