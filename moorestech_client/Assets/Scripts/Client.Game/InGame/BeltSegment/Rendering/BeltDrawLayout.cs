using System.Collections.Generic;
using Game.BeltSegment;
using UnityEngine;
namespace Client.Game.InGame.BeltSegment.Rendering
{
    internal sealed class BeltDrawLayout
    {
        internal readonly Vector4[] Cells, Entries;
        internal readonly Vector2Int[] Routes;
        internal readonly Bounds Bounds;
        internal BeltDrawLayout(BeltRoute[] routes)
        {
            var cells = new List<Vector4>();
            var entries = new List<Vector4>();
            Routes = new Vector2Int[routes.Length];
            var bounds = new Bounds(Vector3.zero, Vector3.one);
            // Core→Unity軸、表面高維持。
            // Map Core to Unity axes; retain surface height.
            for (int i = 0; i < routes.Length; i++)
            {
                Routes[i] = new Vector2Int(cells.Count, routes[i].Cells.Length);
                foreach (var cell in routes[i].Cells)
                {
                    var point = Pack(cell);
                    cells.Add(point);
                    bounds.Encapsulate(new Vector3(point.x, point.y, point.z));
                }
                foreach (var cell in routes[i].EntryCells) entries.Add(Pack(cell));
            }
            Cells = cells.ToArray(); Entries = entries.ToArray();
            bounds.Expand(4); Bounds = bounds;
            #region Internal
            static Vector4 Pack(BeltRouteCell cell) => new(cell.Cell.X, cell.CenterHeightTwice * 0.5f,
                cell.Cell.Y, cell.InputHeightTwice * 0.5f);
            #endregion
        }
    }
}
