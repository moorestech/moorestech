using System;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass
{
    // 障害物クリア下限・隣接差≤1・端点固定を満たす最小のベルト高さプロファイルを求める純粋ロジック
    // Pure logic computing the minimal belt-height profile satisfying clearance lower bounds, adjacency<=1, and fixed endpoints.
    public static class ConveyorVerticalEnvelope
    {
        public static (int[] beltY, bool[] feasible) Solve(int[] lowerBounds, int fixedStart, int fixedEnd, int cornerIndex)
        {
            var n = lowerBounds.Length;
            if (n == 0) return (Array.Empty<int>(), Array.Empty<bool>());

            // 2パスで下限と隣接差≤1を満たす最小プロファイルを求める
            // Two passes give the minimal profile satisfying lower bounds and adjacency<=1.
            var y = TwoPass(lowerBounds);

            // コーナーは勾配を通せないため前後3セルを平坦な踊り場に引き上げて再計算する
            // The corner cannot carry a slope, so raise its 3-cell neighborhood into a flat plateau and re-solve.
            if (cornerIndex >= 1 && cornerIndex <= n - 2 && !(y[cornerIndex - 1] == y[cornerIndex] && y[cornerIndex] == y[cornerIndex + 1]))
            {
                var plateau = Math.Max(y[cornerIndex - 1], Math.Max(y[cornerIndex], y[cornerIndex + 1]));
                var raised = (int[])lowerBounds.Clone();
                raised[cornerIndex - 1] = Math.Max(raised[cornerIndex - 1], plateau);
                raised[cornerIndex] = Math.Max(raised[cornerIndex], plateau);
                raised[cornerIndex + 1] = Math.Max(raised[cornerIndex + 1], plateau);
                y = TwoPass(raised);
            }

            // 端点は固定値。包絡線がそれを超えて上がったらランプを戻しきれない＝設置不可
            // Endpoints are fixed. If the envelope rose above them, the ramp cannot return -> not placeable.
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

            #endregion
        }
    }
}
