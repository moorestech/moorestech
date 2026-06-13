using System.Collections.Generic;

namespace Game.CleanRoom
{
    // 閾値1行ぶんの判定値。マスタ要素からデータストアが初期化時に変換して保持する。
    // One threshold row for decisions; converted once from master elements by the datastore.
    public readonly struct CleanRoomThresholdRow
    {
        public readonly double MaxConcentration;       // 個/m³（降格側の素閾値）
        public readonly double RequiredAirChangeRate;  // 1/秒（降格側の素要求）

        public CleanRoomThresholdRow(double maxConcentration, double requiredAirChangeRate)
        {
            MaxConcentration = maxConcentration;
            RequiredAirChangeRate = requiredAirChangeRate;
        }
    }

    // 純度の判定・積分・按分の純関数群。数値はバランス確定書が唯一ソース。
    // Pure functions for purity decisions, integration, and apportionment.
    public static class CleanRoomPurityRules
    {
        // 昇格側ヒステリシス係数。濃度は×0.8、ACHは×1.25（=÷0.8）を超えて初めて昇格。
        // Promotion-side hysteresis: concentration ×0.8, ACH ×1.25 (anti-flicker for both conditions).
        public const double PromoteConcentrationFactor = 0.8;
        public const double PromoteAirChangeFactor = 1.25;

        // Degraded 猶予秒数（バランス確定書§1.2: 5.0秒 = 100tick）。
        // Grace seconds for Degraded (balance §1.2).
        public const double GraceSeconds = 5.0;

        // 現在行・濃度C・換気ACHから次の閾値行を決める。戻り値 rows.Count は Out。
        // Decide the next threshold row; returning rows.Count means Out.
        public static int DecideThresholdIndex(int currentIndex, double concentration, double airChangeRate,
            IReadOnlyList<CleanRoomThresholdRow> rows)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                // 上位行を狙う（昇格）ときだけ両条件にマージンを掛ける。
                // Apply margins to both conditions only when aiming above the current row.
                var isImprovement = i < currentIndex;
                var concentrationLimit = isImprovement ? rows[i].MaxConcentration * PromoteConcentrationFactor : rows[i].MaxConcentration;
                var achRequired = isImprovement ? rows[i].RequiredAirChangeRate * PromoteAirChangeFactor : rows[i].RequiredAirChangeRate;

                if (concentration <= concentrationLimit && airChangeRate >= achRequired) return i;
            }

            return rows.Count;
        }

        // 1tick分の積分: N' = max(0, N + (A − n·q·(N/V))·dt)。
        // One-tick explicit Euler with zero clamp.
        public static double IntegrateTick(double impurityCount, double volume, double aTotalPerSecond,
            double removalVolumePerSecond, double deltaSeconds)
        {
            var concentration = volume > 0.0 ? impurityCount / volume : 0.0;
            var next = impurityCount + (aTotalPerSecond - removalVolumePerSecond * concentration) * deltaSeconds;
            return next < 0.0 ? 0.0 : next;
        }

        // 再検出按分: N_old·overlap/oldCellCount。分母は |Cells|（Vではない。保存則のため）。
        // Apportionment across re-detection; denominator is |Cells|, NOT V, to conserve N.
        public static double RedistributeImpurity(double oldImpurity, int oldCellCount, int overlapCellCount)
        {
            if (oldCellCount <= 0 || overlapCellCount <= 0) return 0.0;
            return oldImpurity * overlapCellCount / oldCellCount;
        }
    }
}
