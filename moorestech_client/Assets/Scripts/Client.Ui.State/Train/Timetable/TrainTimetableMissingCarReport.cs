using Client.Ui.State.Diagnostics;
using UnityEngine;

namespace Client.Ui.State.Train.Timetable
{
    /// <summary>
    /// 貨車の欠けを同じ間 1 回だけ警告する
    /// Warns once while the same car stays missing
    /// </summary>
    public sealed class TrainTimetableMissingCarReport
    {
        private readonly AnomalyReportGate _gate = new("[TrainTimetableModel] Missing car snapshot: ", "", LogType.Warning);

        public void ReportMissing(long trainCarInstanceId)
        {
            _gate.Observe(trainCarInstanceId.ToString());
        }

        // 欠けが解けたら旗を戻し再警告に備える
        // Clear flag on recovery so it warns again
        public void Clear()
        {
            _gate.Observe(null);
        }
    }
}
