namespace Client.Ui.Foundation.DragDrop
{
    /// <summary>
    /// 落とし先の印に載せる画面の値（枠の番号・枠の間の隙間など）。Foundation は中身を読まず、源の IUiDragPayload へそのまま渡す
    /// A screen value placed on a drop-target mark (slot index, gap between slots, ...); Foundation never reads it and hands it to the source's IUiDragPayload
    /// </summary>
    public interface IUiDropTarget
    {
    }
}
