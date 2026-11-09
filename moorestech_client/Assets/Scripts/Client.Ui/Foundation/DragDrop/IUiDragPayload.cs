namespace Client.Ui.Foundation.DragDrop
{
    /// <summary>
    /// ドラッグの源が持つ値と押下の受け手。落とし先の型を型引数で閉じ、受け手は具体型をそのまま受ける
    /// Value held by a drag source and its press receiver; the drop target type is closed by the type argument so receivers get the concrete type
    /// </summary>
    public interface IUiDragPayload<in TTarget> where TTarget : class
    {
        void OnTapped();
        void OnDropped(TTarget target);
        void OnDroppedOutside();
    }
}
