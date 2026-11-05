namespace Client.Ui.State.Toast
{
    public enum GameUiToastVariant
    {
        Info,
        Error,
    }

    /// <summary>
    /// トースト 1 件の文と種別
    /// Text and variant of one toast
    /// </summary>
    public sealed class GameUiToast
    {
        public string Text { get; }
        public GameUiToastVariant Variant { get; }

        public GameUiToast(string text, GameUiToastVariant variant)
        {
            Text = text;
            Variant = variant;
        }
    }
}
