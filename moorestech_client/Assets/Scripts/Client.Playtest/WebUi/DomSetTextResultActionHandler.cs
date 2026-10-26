using System;
using Client.WebUiHost.Boot.Transport;
using Client.Playtest.UiElements;
using Client.WebUiHost.Game.Actions;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.Playtest.WebUi
{
    /// <summary>
    ///     ・文字入力応答の検証
    ///     ・保留中の文字入力へ転送
    ///     ・Validate text-input responses
    ///     ・Forward to pending text-input requests
    /// </summary>
    internal class DomSetTextResultActionHandler : IConnectionActionHandler
    {
        private readonly PlaytestWebUiRequestChannel<DomSetTextResult> _channel;

        public DomSetTextResultActionHandler(PlaytestWebUiRequestChannel<DomSetTextResult> channel)
        {
            _channel = channel;
        }

        public string ActionType => "playtest.dom_set_text_result";

        public UniTask<ActionResult> ExecuteAsync(Guid connectionId, JObject payload)
        {
            // WS外部入力を型ごとに検証し、未知の状態語は契約不整合として拒否する
            // Validate external WS input and reject unknown status words as contract mismatches
            if (!WebUiPayloadReader.TryReadNonEmptyString(payload, "requestId", out var requestId))
            {
                Debug.LogWarning("[PlaytestUi] Rejected response without requestId");
                return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            }
            if (!_channel.Accepts(requestId, connectionId))
                return UniTask.FromResult(ActionResult.Fail("unrelated_response"));
            if (!WebUiPayloadReader.TryReadString(payload, "status", out var statusWord) ||
                !TryParseStatus(statusWord, out var status) ||
                !WebUiPayloadReader.TryReadNonNegativeInt(payload, "matchCount", out var matchCount) ||
                !WebUiPayloadReader.TryReadString(payload, "actualValue", out var actualValue))
            {
                Debug.LogWarning("[PlaytestUi] Rejected malformed playtest.dom_set_text_result payload");
                _channel.Reject(requestId, "Malformed or inconsistent DomSetTextResult payload");
                return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            }
            if ((status == UiSetTextStatus.NotFound) != (matchCount == 0) ||
                (status == UiSetTextStatus.Ambiguous) != (matchCount > 1))
            {
                Debug.LogWarning("[PlaytestUi] Rejected inconsistent playtest.dom_set_text_result match count");
                _channel.Reject(requestId, "Malformed or inconsistent DomSetTextResult payload");
                return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            }

            _channel.Resolve(requestId, new DomSetTextResult { Status = status, MatchCount = matchCount, ActualValue = actualValue });
            return UniTask.FromResult(ActionResult.Success());

            #region Internal

            bool TryParseStatus(string word, out UiSetTextStatus status)
            {
                switch (word)
                {
                    case "ambiguous": status = UiSetTextStatus.Ambiguous; return true;
                    case "applied": status = UiSetTextStatus.Applied; return true;
                    case "notFound": status = UiSetTextStatus.NotFound; return true;
                    case "notTextField": status = UiSetTextStatus.NotTextField; return true;
                    case "notInteractable": status = UiSetTextStatus.NotInteractable; return true;
                    case "valueMismatch": status = UiSetTextStatus.ValueMismatch; return true;
                    default: status = UiSetTextStatus.RendererUnavailable; return false;
                }
            }

            #endregion
        }
    }
}
