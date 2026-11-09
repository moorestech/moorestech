namespace Client.Ui.Foundation.Input.OsCursor
{
    /// <summary>
    /// OS カーソルの形を切り替える唯一の口（USS はビットマップを参照できないため C# から当てる。D-12）
    /// The only port that switches the OS cursor shape (USS may not reference bitmaps, so C# applies it; D-12)
    /// </summary>
    public interface IUiOsCursor
    {
        void SetShape(UiOsCursorShape shape);
    }
}
