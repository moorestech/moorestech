using System;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass
{
    // 障害物クリア下限・隣接差≤1・端点固定を満たし、障害物間の狭すぎる谷は橋渡しに引き上げる高さプロファイルを求める純粋ロジック
    // Pure logic for a belt-height profile satisfying clearance bounds, adjacency<=1, fixed endpoints, while bridging gaps too narrow to descend into.
    public static class ConveyorVerticalEnvelope
    {
        // 地面まで降りて平坦区間を作ったと見なす最小セル数。これ未満のギャップは橋渡しする
        // Minimum flat-floor cells that count as a real descent; gaps narrower than this are bridged.
        private const int MinFlatFloor = 1;

        public static (int[] beltY, bool[] feasible) Solve(int[] lowerBounds, int fixedStart, int fixedEnd, int cornerIndex)
        {
            var n = lowerBounds.Length;
            if (n == 0) return (Array.Empty<int>(), Array.Empty<bool>());

            var working = (int[])lowerBounds.Clone();

            // コーナーは勾配を通せないため前後3セルを踊り場として下限に反映する
            // The corner cannot carry a slope, so pin its 3-cell neighborhood into a plateau lower bound.
            ApplyCornerPlateau();

            // 障害物間の狭すぎる谷を橋渡し高さへ引き上げてから最小高さを確定する
            // Bridge gaps too narrow to descend into, then settle on the minimal height.
            var y = TwoPass(working);
            while (BridgeNarrowBasins(y)) y = TwoPass(working);

            // 端点は固定値。包絡線がそれを超えたらランプを戻しきれない＝設置不可
            // Endpoints are fixed; if the envelope exceeds them the ramp cannot return -> infeasible.
            var feasible = new bool[n];
            for (var i = 0; i < n; i++) feasible[i] = true;
            feasible[0] = y[0] == fixedStart;
            feasible[n - 1] = y[n - 1] == fixedEnd;

            return (y, feasible);

            #region Internal

            int[] TwoPass(int[] bounds)
            {
                var r = (int[])bounds.Clone();
                for (var i = 1; i < n; i++) r[i] = Math.Max(r[i], r[i - 1] - 1);
                for (var i = n - 2; i >= 0; i--) r[i] = Math.Max(r[i], r[i + 1] - 1);
                return r;
            }

            void ApplyCornerPlateau()
            {
                if (cornerIndex < 1 || cornerIndex > n - 2) return;
                var y0 = TwoPass(working);
                if (y0[cornerIndex - 1] == y0[cornerIndex] && y0[cornerIndex] == y0[cornerIndex + 1]) return;
                var plateau = Math.Max(y0[cornerIndex - 1], Math.Max(y0[cornerIndex], y0[cornerIndex + 1]));
                working[cornerIndex - 1] = Math.Max(working[cornerIndex - 1], plateau);
                working[cornerIndex] = Math.Max(working[cornerIndex], plateau);
                working[cornerIndex + 1] = Math.Max(working[cornerIndex + 1], plateau);
            }

            bool BridgeNarrowBasins(int[] profile)
            {
                // 各セルの左右最大値から「水が溜まる窪み(谷)」を検出する（雨水トラップと同型）
                // Detect trapped basins from per-cell left/right maxima (same shape as the trapping-rain-water problem).
                var prefMax = new int[n];
                var sufMax = new int[n];
                prefMax[0] = profile[0];
                for (var i = 1; i < n; i++) prefMax[i] = Math.Max(prefMax[i - 1], profile[i]);
                sufMax[n - 1] = profile[n - 1];
                for (var i = n - 2; i >= 0; i--) sufMax[i] = Math.Max(sufMax[i + 1], profile[i]);

                var changed = false;
                var idx = 0;
                while (idx < n)
                {
                    // 窪みでないセルは読み飛ばす（窪み=水位より低いセル）
                    // Skip non-basin cells (a basin cell is below its trapped water level).
                    if (profile[idx] >= Math.Min(prefMax[idx], sufMax[idx])) { idx++; continue; }

                    // 窪みの連続区間[l..r]と、両肩(rim)・床(floor)・幅(gap)を求める
                    // Find the contiguous basin [l..r] with its rims, floor, and gap width.
                    var l = idx;
                    var r = idx;
                    while (r + 1 < n && profile[r + 1] < Math.Min(prefMax[r + 1], sufMax[r + 1])) r++;

                    var leftRim = prefMax[l - 1];
                    var rightRim = sufMax[r + 1];
                    var baseFloor = profile[l];
                    for (var k = l; k <= r; k++) baseFloor = Math.Min(baseFloor, profile[k]);

                    // 地面まで降りて平坦区間を作るのに要する幅。足りなければ低い側の肩高さで橋渡しする
                    // Width needed to descend to the floor with a flat run; if short, bridge at the lower rim height.
                    var requiredGap = (leftRim - baseFloor) + (rightRim - baseFloor) + MinFlatFloor;
                    if (r - l + 1 < requiredGap)
                    {
                        var bridge = Math.Min(leftRim, rightRim);
                        for (var k = l; k <= r; k++) working[k] = Math.Max(working[k], bridge);
                        changed = true;
                    }

                    idx = r + 1;
                }

                return changed;
            }

            #endregion
        }
    }
}
