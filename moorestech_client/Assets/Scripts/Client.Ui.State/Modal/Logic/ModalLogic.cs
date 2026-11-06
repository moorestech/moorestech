namespace Client.Ui.State.Modal.Logic
{
    /// <summary>
    /// モーダルの確定可否と色の判定（Web modalLogic.ts の canConfirm・buttonColor の写し）
    /// Modal confirm eligibility and colour (port of the Web modalLogic.ts canConfirm and buttonColor)
    /// </summary>
    public static class ModalLogic
    {
        // 入力付きモーダルは空白だけの文を確定させない（uGUI BlueprintNameInputView と同じ検証）
        // An input modal rejects whitespace-only text (same validation as uGUI BlueprintNameInputView)
        public static bool CanConfirm(ModalRequest request, string enteredText)
        {
            if (!request.RequiresInput) return true;
            return enteredText.Trim().Length > 0;
        }

        // エラーは赤、確認は青（uGUI の色分けと同じ）
        // Error is red and confirm is blue, mirroring the uGUI styling
        public static ModalButtonTone ConfirmButtonTone(ModalVariant variant)
        {
            return variant == ModalVariant.Error ? ModalButtonTone.Red : ModalButtonTone.Blue;
        }
    }
}
