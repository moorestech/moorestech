namespace Client.Game.InGame.UI.Modal
{
    /// <summary>
    /// 保留中モーダル要求の内容
    /// Contents of a pending modal request
    /// </summary>
    public class ModalRequest
    {
        public string Id;
        public string Title;
        public string Message;
        public string ButtonText;
        public ModalVariant Variant;
        public bool RequiresInput;
    }

    /// <summary>
    /// モーダルの表示種別。web の "confirm" | "error" に対応
    /// Modal display kind; maps to the web's "confirm" | "error"
    /// </summary>
    public enum ModalVariant
    {
        Confirm,
        Error,
    }
}
