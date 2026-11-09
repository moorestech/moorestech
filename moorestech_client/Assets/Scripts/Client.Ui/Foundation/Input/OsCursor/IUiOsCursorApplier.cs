namespace Client.Ui.Foundation.Input.OsCursor
{
    /// <summary>
    /// 要求の調停で決まった形を OS カーソルへ当てる先（本番は Cursor.SetCursor、テストは記録だけ）
    /// Where the shape settled by request arbitration is applied to the OS cursor (Cursor.SetCursor in production, recording in tests)
    /// </summary>
    public interface IUiOsCursorApplier
    {
        void Apply(UiOsCursorShape shape);
    }
}
