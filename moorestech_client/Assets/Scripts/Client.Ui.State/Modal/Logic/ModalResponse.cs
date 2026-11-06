namespace Client.Ui.State.Modal.Logic
{
    /// <summary>
    /// モーダルへの応答。入力文を運ぶのは入力付きモーダルの確定だけ（Web respondPayload の写し）
    /// A reply to a modal; only an input modal's confirm carries text (port of the Web respondPayload)
    /// </summary>
    public sealed class ModalResponse
    {
        public string RequestId { get; }
        public ModalResult Result { get; }

        // null は文なし。ModalRequestService.Respond の text へそのまま渡す
        // Null means no text; passed straight to ModalRequestService.Respond's text
        public string Text { get; }

        private ModalResponse(string requestId, ModalResult result, string text)
        {
            RequestId = requestId;
            Result = result;
            Text = text;
        }

        public static ModalResponse Confirm(ModalRequest request, string enteredText)
        {
            // 入力なしのモーダルでは入力欄の残り文を運ばない
            // A modal without input never carries leftover field text
            return new ModalResponse(request.Id, ModalResult.Confirm, request.RequiresInput ? enteredText : null);
        }

        public static ModalResponse Cancel(ModalRequest request)
        {
            return new ModalResponse(request.Id, ModalResult.Cancel, null);
        }
    }
}
