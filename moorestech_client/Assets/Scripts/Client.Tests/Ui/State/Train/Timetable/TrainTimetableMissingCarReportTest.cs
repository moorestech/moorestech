using Client.Ui.State.Train.Timetable;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.Train.Timetable
{
    /// <summary>
    /// 貨車の欠けは同じ貨車の間 1 回だけ警告し、回復後は再び警告する
    /// A missing car warns once while it stays missing and warns again after recovery
    /// </summary>
    public class TrainTimetableMissingCarReportTest
    {
        [Test]
        public void 同じ貨車の欠けは1回で_回復後と別の貨車はまた出る()
        {
            var report = new TrainTimetableMissingCarReport();

            LogAssert.Expect(LogType.Warning, "[TrainTimetableModel] Missing car snapshot: 5");
            report.ReportMissing(5);
            report.ReportMissing(5);

            // 別の貨車は新しい異常として出る
            // A different car is a new anomaly
            LogAssert.Expect(LogType.Warning, "[TrainTimetableModel] Missing car snapshot: 6");
            report.ReportMissing(6);

            // 回復で旗が戻る
            // Recovery clears the flag
            report.Clear();
            LogAssert.Expect(LogType.Warning, "[TrainTimetableModel] Missing car snapshot: 6");
            report.ReportMissing(6);
        }
    }
}
