namespace Client.Ui.Foundation.DragDrop
{
    /// <summary>
    /// ドラッグの源が持つ値と、押下の結果の受け手。解決（割当・入替・解除など）は源の側が決める（Web resolveDropAction(source, target)）
    /// The value a drag source carries and the receiver of the press outcome; the source side resolves what happens (Web resolveDropAction(source, target))
    /// </summary>
    public interface IUiDragPayload
    {
        void OnTapped();
        void OnDropped(IUiDropTarget target);
        void OnDroppedOutside();
    }
}
