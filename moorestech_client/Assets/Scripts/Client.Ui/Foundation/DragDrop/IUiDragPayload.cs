namespace Client.Ui.Foundation.DragDrop
{
    /// <summary>
    /// ドラッグの源が持つ値と押下の受け手
    /// Value held by a drag source and its press receiver
    /// </summary>
    public interface IUiDragPayload
    {
        void OnTapped();
        void OnDropped(IUiDropTarget target);
        void OnDroppedOutside();
    }
}
