using System;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.Modal
{
    /// <summary>
    /// 出したモーダル要求の持ち手。Dispose は自分の要求だけを取り消す
    /// Handle of an issued modal request; Dispose cancels only its own request
    /// </summary>
    public sealed class ModalRequestHandle : IDisposable
    {
        public string RequestId { get; }

        // 応答・取り消し・置き換えのどれでも解決する（取り消しと置き換えは Cancel で text は null）
        // Resolves on reply, cancellation or replacement (cancellation and replacement give Cancel with a null text)
        public UniTask<(ModalResult result, string text)> Response { get; }

        private readonly ModalRequestService _service;

        public ModalRequestHandle(ModalRequestService service, string requestId, UniTask<(ModalResult result, string text)> response)
        {
            _service = service;
            RequestId = requestId;
            Response = response;
        }

        public void Dispose()
        {
            // 応答済み・置き換え済みの ID は Respond が無視するので、別の要求は閉じない
            // Respond ignores answered or replaced ids, so no other request is closed
            _service.Respond(RequestId, ModalResult.Cancel, null);
        }
    }
}
