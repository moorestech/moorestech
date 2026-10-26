using Client.Playtest.UiElements;
using Client.WebUiHost.Game.Actions;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.Playtest.WebUi
{
    /// <summary>
    ///     playtest.dom_set_text_result を検証して保留中の文字入力へ渡す
    ///     Validates playtest.dom_set_text_result and hands it to the pending text input
    /// </summary>
    internal class DomSetTextResultActionHandler : IActionHandler
    {
        private readonly PlaytestWebUiRequestChannel<DomSetTextResult> _channel;

        public DomSetTextResultActionHandler(PlaytestWebUiRequestChannel<DomSetTextResult> channel)
        {
            _channel = channel;
        }

        public string ActionType => "playtest.dom_set_text_result";

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            // WS外部入力を型ごとに検証し、未知の状態語は契約不整合として拒否する
            // Validate external WS input and reject unknown status words as contract mismatches
            if (!WebUiPayloadReader.TryReadNonEmptyString(payload, "requestId", out var requestId) ||
                !WebUiPayloadReader.TryReadString(payload, "status", out var statusWord) ||
                !TryParseStatus(statusWord, out var status) ||
                !WebUiPayloadReader.TryReadNonNegativeInt(payload, "matchCount", out var matchCount) ||
                !WebUiPayloadReader.TryReadString(payload, "actualValue", out var actualValue))
            {
                Debug.LogWarning("[PlaytestUi] Rejected malformed playtest.dom_set_text_result payload");
                return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            }
            if ((status == UiSetTextStatus.NotFound) != (matchCount == 0))
            {
                Debug.LogWarning("[PlaytestUi] Rejected inconsistent playtest.dom_set_text_result match count");
                return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            }

            _channel.Resolve(requestId, new DomSetTextResult { Status = status, MatchCount = matchCount, ActualValue = actualValue });
            return UniTask.FromResult(ActionResult.Success());
        }

        private static bool TryParseStatus(string word, out UiSetTextStatus status)
        {
            switch (word)
            {
                case "applied": status = UiSetTextStatus.Applied; return true;
                case "notFound": status = UiSetTextStatus.NotFound; return true;
                case "notTextField": status = UiSetTextStatus.NotTextField; return true;
                case "notInteractable": status = UiSetTextStatus.NotInteractable; return true;
                case "valueMismatch": status = UiSetTextStatus.ValueMismatch; return true;
                default: status = UiSetTextStatus.RendererUnavailable; return false;
            }
        }
    }
}
