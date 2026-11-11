namespace Client.Ui.State.Modal.Logic
{
    /// <summary>
    /// モーダルの確定可否と色の判定（Web modalLogic.ts の canConfirm・buttonColor の写し）
    /// Modal confirm eligibility and colour (port of the Web modalLogic.ts canConfirm and buttonColor)
    /// </summary>
    public static class ModalLogic
    {
        // 可否と確定の文を 1 回で導く。入力付きは空白だけを弾き前後の空白を除いた文、入力なしは文を運ばない（Web text.trim()・uGUI と同じ検証）
        // Derive eligibility and the confirmed text in one pass; input modals reject whitespace-only and carry the trimmed text, others carry none (Web text.trim(), same as uGUI)
        public static bool TryGetConfirmableText(ModalRequest request, string enteredText, out string confirmedText)
        {
            confirmedText = null;
            if (!request.RequiresInput) return true;
            var trimmed = Trim(enteredText);
            if (!IsConfirmable(trimmed)) return false;
            confirmedText = trimmed;
            return true;
        }

        // 空白だけでない文か。BP 名の書き戻しが同じ規則を読む
        // Whether the text is more than whitespace; the blueprint-name write-back reads the same rule
        public static bool HasConfirmableText(string enteredText)
        {
            return IsConfirmable(Trim(enteredText));
        }

        private static bool IsConfirmable(string trimmed)
        {
            return 0 < trimmed.Length;
        }

        private static string Trim(string enteredText)
        {
            return enteredText == null ? "" : enteredText.Trim();
        }

        // エラーは赤、確認は青（uGUI の色分けと同じ）
        // Error is red and confirm is blue, mirroring the uGUI styling
        public static ModalButtonTone ConfirmButtonTone(ModalVariant variant)
        {
            return variant == ModalVariant.Error ? ModalButtonTone.Red : ModalButtonTone.Blue;
        }
    }
}
