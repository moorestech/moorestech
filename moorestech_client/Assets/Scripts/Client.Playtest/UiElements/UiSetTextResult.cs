namespace Client.Playtest.UiElements
{
    public readonly struct UiSetTextResult
    {
        public readonly UiSetTextStatus Status;
        public readonly UiRenderer Renderer;
        // 設定を試みた後の欄の値（欄が無ければ空）
        // The field value after the attempt (empty when there is no field)
        public readonly string ActualValue;

        public UiSetTextResult(UiSetTextStatus status, UiRenderer renderer, string actualValue)
        {
            Status = status;
            Renderer = renderer;
            ActualValue = actualValue;
        }
    }
}
