using System.Collections.Generic;
using Mooresmaster.Loader.CleanRoomThresholdsModule;
using Mooresmaster.Model.CleanRoomThresholdsModule;
using Newtonsoft.Json.Linq;

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

        public CleanRoomThresholdMaster(JToken jToken)
        {
            CleanRoomThresholds = CleanRoomThresholdsLoader.Load(jToken);
        }

        public bool Validate(out string errorLogs)
        {
            // 行が空だと閾値判定・Out インデックスが成立しない。
            // An empty table makes threshold decisions and the Out index meaningless.
            if (Rows.Count == 0)
            {
                errorLogs = "cleanRoomThresholds must have at least one row";
                return false;
            }

            // 各行の値域を確認（DownBinRate は確率、MaxGrade は非負グレード）。
            // Validate per-row ranges (DownBinRate is a probability, MaxGrade is a non-negative grade).
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
            }

            // 行の単調性（濃度昇順・必要換気降順）を強制。ヒステリシス判定の前提。
            // Enforce monotonic rows (concentration asc, required ACH desc); hysteresis relies on this.
            for (var i = 1; i < Rows.Count; i++)
            {
                if (Rows[i].MaxConcentration <= Rows[i - 1].MaxConcentration ||
                    Rows[i].RequiredAirChangeRate >= Rows[i - 1].RequiredAirChangeRate)
                {
                    errorLogs = $"cleanRoomThresholds rows must be sorted (cleanest first). row={i}";
                    return false;
                }
            }

            errorLogs = null;
            return true;
        }

        public void Initialize() { }
    }
}
