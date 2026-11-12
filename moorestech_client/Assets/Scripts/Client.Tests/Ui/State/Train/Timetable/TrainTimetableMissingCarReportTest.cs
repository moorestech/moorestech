using Client.Ui.State.Train.Timetable;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.Train.Timetable
{
    /// <summary>
    /// 貨車の欠けは同じ間 1 回、回復後に再警告
    /// Missing car warns once; again after recovery
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

            // 2 回目の警告は未期待ログで落ちる
            // A second warning fails as an unexpected log
            LogAssert.NoUnexpectedReceived();

            // 別の貨車は新しい異常として出る
            // A different car is a new anomaly
            LogAssert.Expect(LogType.Warning, "[TrainTimetableModel] Missing car snapshot: 6");
            report.ReportMissing(6);

            // 回復で旗が戻る
            // Recovery clears the flag
            report.Clear();
            LogAssert.Expect(LogType.Warning, "[TrainTimetableModel] Missing car snapshot: 6");
            report.ReportMissing(6);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
