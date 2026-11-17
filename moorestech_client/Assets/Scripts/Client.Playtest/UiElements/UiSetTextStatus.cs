namespace Client.Playtest.UiElements
{
    public enum UiSetTextStatus
    {
        Applied,
        NotFound,
        // TextField以外
        // Not a TextField
        NotTextField,
        // 非表示・無効・読み取り専用で今は入力できない
        // Hidden, disabled, or read-only, so input is not possible now
        NotInteractable,
        // 設定後の値が要求と違う（文字数上限で切られた等）
        // The value after setting differs from the request (e.g. truncated by a length limit)
        ValueMismatch,
        Ambiguous,
        RendererUnavailable,
    }
}
