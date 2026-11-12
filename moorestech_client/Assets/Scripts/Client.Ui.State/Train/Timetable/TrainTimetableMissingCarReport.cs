using Client.Ui.State.Diagnostics;
using UnityEngine;

namespace Client.Ui.State.Train.Timetable
{
    /// <summary>
    /// 開いた貨車のスナップショットが欠けている異常を、同じ貨車の間は 1 回だけ警告する
    /// Warns once about a missing car snapshot while the same car stays missing
    /// </summary>
    public sealed class TrainTimetableMissingCarReport
    {
        private readonly AnomalyReportGate _gate = new();

        public void ReportMissing(long trainCarInstanceId)
        {
            var message = $"[TrainTimetableModel] Missing car snapshot: {trainCarInstanceId}";
            if (_gate.ShouldReport(message)) Debug.LogWarning(message);
        }

        // 欠けが解けた・貨車を開いていない間は旗を戻し、回復後の同じ異常をもう一度出せるようにする
        // Clear the flag once the car resolves or none is open so the same anomaly is reported again after recovery
        public void Clear()
        {
            _gate.ShouldReport(null);
        }
    }
}
