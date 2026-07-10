using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// ui.modal.respond: Web からのモーダル応答（confirm | cancel）を反映する
    /// ui.modal.respond: apply a modal reply (confirm | cancel) from the web
    /// </summary>
    public class ModalRespondActionHandler : IActionHandler
    {
        public string ActionType => "ui.modal.respond";

        private readonly WebUiModalService _service;

        public ModalRespondActionHandler(WebUiModalService service)
        {
            _service = service;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));

            // id と result（confirm | cancel）を検証する
            // Validate id and result (confirm | cancel)
            if (payload["id"] is not JValue { Type: JTokenType.String } idValue) return UniTask.FromResult(ActionResult.Fail("invalid_id"));
            if (payload["result"] is not JValue { Type: JTokenType.String } resultValue) return UniTask.FromResult(ActionResult.Fail("invalid_result"));

            var result = (string)resultValue;
            if (result != "confirm" && result != "cancel") return UniTask.FromResult(ActionResult.Fail("invalid_result"));

            // 入力モーダルの text は任意（無ければ null）
            // The input modal's text is optional (null when absent)
            var text = payload["text"] is JValue { Type: JTokenType.String } textValue ? (string)textValue : null;

            // id 不一致・保留なしは古い応答なので no_pending_modal で返す
            // id mismatch or no pending request is a stale reply; report no_pending_modal
            if (!_service.Respond((string)idValue, result, text)) return UniTask.FromResult(ActionResult.Fail("no_pending_modal"));
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}
