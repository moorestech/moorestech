using System;
using Core.BeltTransport;
using UnityEngine;
namespace Client.Game.InGame.BeltTransport
{
    public static class BeltItemPosition
    {
        public static Vector3 Calculate(BeltNetworkSnapshot snapshot, BeltCellItemState item)
        {
            var cell = snapshot.Cells[Array.FindIndex(snapshot.Cells, value => value.Id == item.CellId)];
            var entry = item.EntryDirection switch
            {
                BeltDirection.Front => Vector3.forward,
                BeltDirection.Back => Vector3.back,
                BeltDirection.Left => Vector3.left,
                BeltDirection.Right => Vector3.right,
                _ => throw new ArgumentException($"Invalid belt entry direction: {item.EntryDirection}.")
            };
            float progress = item.IsBuffer ? 1f : item.Progress / (float)BeltConstants.ItemWidth;
            float midpoint = (cell.Surface.InputHeight + cell.Surface.OutputHeight) * 0.5f;
            float height = cell.Y + Mathf.Lerp(cell.Surface.InputHeight, midpoint, Mathf.Max(0, 2f * progress - 1f));
            // 進行前半は搬入元の後半面、後半は現セルの前半面に沿う。
            // Follow the source's rear half before the edge and the current cell's front half after it.
            if (progress < 0.5f)
            {
                foreach (var edge in snapshot.Connections)
                {
                    if (!edge.SourceIsBelt || edge.TargetId != cell.Id || (int)edge.Direction != ((int)item.EntryDirection ^ 1)) continue;
                    int index = Array.FindIndex(snapshot.Cells, value => value.Id == edge.SourceId);
                    if (index < 0) continue;
                    var source = snapshot.Cells[index];
                    float sourceMidpoint = (source.Surface.InputHeight + source.Surface.OutputHeight) * 0.5f;
                    height = source.Y + Mathf.Lerp(sourceMidpoint, source.Surface.OutputHeight, 2f * progress);
                    break;
                }
            }
            // 元セル撤去後や機械入力では、現セル入口面を外側へ延ばす。
            // Without a source belt, extend the current entry surface outside the cell.
            var center = new Vector3(cell.X + 0.5f, height + 0.35f, cell.Z + 0.5f);
            return item.IsBuffer ? center : center + entry * (1f - progress);
        }
    }
}
