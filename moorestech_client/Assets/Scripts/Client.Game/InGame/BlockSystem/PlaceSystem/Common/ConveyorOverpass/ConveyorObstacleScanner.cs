using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass
{
    // 経路各セルの直上に積み上がった既存ブロックを調べ、跨ぐのに必要な最小ベルト高さ下限を返す
    // Scans existing blocks stacked above each path cell and returns the minimal belt-height lower bound to clear them.
    public class ConveyorObstacleScanner
    {
        // 無限ループ防止の安全上限
        // Safety cap to prevent an infinite scan loop.
        private const int MaxScanHeight = 64;

        public int[] ComputeLowerBounds(IReadOnlyList<Vector3Int> cells, Func<Vector3Int, bool> isOccupied)
        {
            var bounds = new int[cells.Count];
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];

                // 基準Yから連続して占有されている高さを上に辿り、その直上を下限とする
                // Walk up the contiguous occupied stack from base Y; the cell just above becomes the lower bound.
                var y = cell.y;
                var scanned = 0;
                while (scanned < MaxScanHeight && isOccupied(new Vector3Int(cell.x, y, cell.z)))
                {
                    y++;
                    scanned++;
                }

                bounds[i] = y;
            }
            return bounds;
        }
    }
}
