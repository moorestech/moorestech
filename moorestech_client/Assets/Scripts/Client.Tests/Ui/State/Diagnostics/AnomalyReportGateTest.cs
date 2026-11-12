using Client.Ui.State.Diagnostics;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Diagnostics
{
    public class AnomalyReportGateTest
    {
        [Test]
        public void 同じ異常は1回だけで回復後はまた出る()
        {
            var gate = new AnomalyReportGate();

            Assert.IsFalse(gate.ShouldReport(null));
            Assert.IsTrue(gate.ShouldReport("a"));
            Assert.IsFalse(gate.ShouldReport("a"));

            // 別の異常は新しい異常として出る
            // A different anomaly is reported as new
            Assert.IsTrue(gate.ShouldReport("b"));

            // 回復で旗が戻る
            // Recovery clears the flag
            Assert.IsFalse(gate.ShouldReport(null));
            Assert.IsTrue(gate.ShouldReport("b"));
        }
    }
}
