using Client.WebUiHost.Game.Actions;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.Playtest.WebUi
{
    /// <summary>
    ///     ・DOM照会応答の検証
    ///     ・保留中の照会へ転送
    ///     ・Validate DOM query responses
    ///     ・Forward to pending queries
    /// </summary>
    internal class DomQueryResultActionHandler : IActionHandler
    {
        private readonly PlaytestWebUiRequestChannel<DomQueryResult> _channel;

        public DomQueryResultActionHandler(PlaytestWebUiRequestChannel<DomQueryResult> channel)
        {
            _channel = channel;
        }

        public string ActionType => "playtest.dom_query_result";

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            // WS外部入力を型ごとに検証し、不正な応答を保留へ混入させない
            // Validate each externally sourced WS value before admitting it to pending
            if (!WebUiPayloadReader.TryReadNonEmptyString(payload, "requestId", out var requestId) ||
                !WebUiPayloadReader.TryReadBool(payload, "found", out var found) ||
                !WebUiPayloadReader.TryReadBool(payload, "visible", out var visible) ||
                !WebUiPayloadReader.TryReadFloat(payload, "x", out var x) ||
                !WebUiPayloadReader.TryReadFloat(payload, "y", out var y) ||
                !WebUiPayloadReader.TryReadFloat(payload, "width", out var width) ||
                !WebUiPayloadReader.TryReadFloat(payload, "height", out var height) ||
                !WebUiPayloadReader.TryReadFloat(payload, "devicePixelRatio", out var devicePixelRatio) ||
                !WebUiPayloadReader.TryReadBool(payload, "hitTestPassed", out var hitTestPassed) ||
                !WebUiPayloadReader.TryReadString(payload, "text", out var text) ||
                !WebUiPayloadReader.TryReadNonNegativeInt(payload, "matchCount", out var matchCount) ||
                !WebUiPayloadReader.TryReadStringArray(payload, "stateFlags", out var stateFlags))
            {
                Debug.LogWarning("[PlaytestUi] Rejected malformed playtest.dom_query_result payload");
                return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            }

            // 相関制約を保留への格納前に評価する
            // Evaluate cross-field constraints before storing as pending
            var result = new DomQueryResult
            {
                Found = found,
                Visible = visible,
                X = x,
                Y = y,
                Width = width,
                Height = height,
                DevicePixelRatio = devicePixelRatio,
                HitTestPassed = hitTestPassed,
                Text = text,
                MatchCount = matchCount,
                StateFlags = stateFlags,
            };
            if (!PlaytestDomQuery.IsValidResult(result))
            {
                Debug.LogWarning("[PlaytestUi] Rejected inconsistent playtest.dom_query_result payload");
                return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            }
            _channel.Resolve(requestId, result);
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}
