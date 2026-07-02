using System;
using System.Collections.Generic;
using Mooresmaster.Model.CleanRoomThresholdsModule;

namespace Core.Master
{
    // クリーンルーム閾値テーブル。行0が最良、行数=Out。
    // Clean room threshold table; row 0 is cleanest, row count means Out.
    public class CleanRoomThresholdMaster : IMasterValidator
    {
        public readonly CleanRoomThresholds CleanRoomThresholds;

        // 行0が最良（A相当）。行数がOutのインデックス。
        // Row 0 is cleanest (class A); row count is the Out threshold index.
        public IReadOnlyList<CleanRoomThresholdMasterElement> Rows => CleanRoomThresholds.Data;
        public int OutThresholdIndex => Rows.Count;

        public CleanRoomThresholdMaster(CleanRoomThresholds cleanRoomThresholds)
        {
            // blocks.jsonのoptionalキー未定義（null）は空テーブルへ正規化し、以降はnull無し前提で扱う。
            // Normalize a missing optional key in blocks.json (null) to an empty table so everything downstream stays null-free.
            CleanRoomThresholds = cleanRoomThresholds ?? new CleanRoomThresholds(Array.Empty<CleanRoomThresholdMasterElement>());
        }

        public bool Validate(out string errorLogs)
        {
            // 空テーブルは機能を使わないModのopt-out（optionalキー未定義の場合を含む）。全部屋が常時Outになるだけで安全。
            // An empty table is a valid opt-out (incl. mods omitting the optional key); rooms just stay Out and nothing crashes.
            if (Rows.Count == 0)
            {
                errorLogs = null;
                return true;
            }

            // 各行の値域を確認（DownBinRate は確率、MaxGrade は非負、濃度/必要換気は有限非負）。
            // Validate per-row ranges (DownBinRate probability, MaxGrade non-negative, concentration/ACH finite & non-negative).
            for (var i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].DownBinRate < 0 || Rows[i].DownBinRate > 1)
                {
                    errorLogs = $"cleanRoomThresholds downBinRate must be in [0,1] but was {Rows[i].DownBinRate}. row={i}";
                    return false;
                }
                if (Rows[i].MaxGrade < 0)
                {
                    errorLogs = $"cleanRoomThresholds maxGrade must be >= 0 but was {Rows[i].MaxGrade}. row={i}";
                    return false;
                }
                if (!IsFiniteNonNegative(Rows[i].MaxConcentration) || !IsFiniteNonNegative(Rows[i].RequiredAirChangeRate))
                {
                    errorLogs = $"cleanRoomThresholds maxConcentration/requiredAirChangeRate must be finite & >= 0. row={i}";
                    return false;
                }
            }

            // 行の単調性。濃度昇順・必要換気降順に加え、汚い行ほどグレード非増加・down-bin非減少を強制。
            // Monotonic rows: concentration asc, ACH desc, plus MaxGrade non-increasing and DownBinRate non-decreasing as rows get dirtier.
            for (var i = 1; i < Rows.Count; i++)
            {
                if (Rows[i].MaxConcentration <= Rows[i - 1].MaxConcentration ||
                    Rows[i].RequiredAirChangeRate >= Rows[i - 1].RequiredAirChangeRate)
                {
                    errorLogs = $"cleanRoomThresholds rows must be sorted (cleanest first). row={i}";
                    return false;
                }
                if (Rows[i].MaxGrade > Rows[i - 1].MaxGrade || Rows[i].DownBinRate < Rows[i - 1].DownBinRate)
                {
                    errorLogs = $"cleanRoomThresholds dirtier rows must not raise maxGrade nor lower downBinRate. row={i}";
                    return false;
                }
            }

            errorLogs = null;
            return true;

            #region Internal

            bool IsFiniteNonNegative(float v) => !float.IsNaN(v) && !float.IsInfinity(v) && v >= 0;

            #endregion
        }

        public void Initialize() { }
    }
}
