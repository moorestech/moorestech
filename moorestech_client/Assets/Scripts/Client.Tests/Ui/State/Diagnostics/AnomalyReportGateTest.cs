using Client.Ui.State.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.Diagnostics
{
    /// <summary>
    /// 同じ異常は継続中 1 回だけログへ出し、回復（null の読み）で旗が戻る（L0）
    /// The same anomaly is logged once while it persists and a null reading clears the flag (L0)
    /// </summary>
    public class AnomalyReportGateTest
    {
        [Test]
        public void 同じ異常は継続中1回だけで回復後はまた出る()
        {
            var gate = new AnomalyReportGate("[Tag] ", "; suffix", LogType.Error);

            // 異常なしの読みは何も出さない
            // A healthy reading logs nothing
            gate.Observe(null);
            LogAssert.NoUnexpectedReceived();

            // 同じ異常が続く間は 1 回だけ、頭と後置文を付けて出す
            // The same anomaly is logged once with the prefix and suffix while it persists
            LogAssert.Expect(LogType.Error, "[Tag] a; suffix");
            gate.Observe("a");
            gate.Observe("a");
            LogAssert.NoUnexpectedReceived();

            // 別の異常は新しい異常として出る
            // A different anomaly is logged as new
            LogAssert.Expect(LogType.Error, "[Tag] b; suffix");
            gate.Observe("b");
            LogAssert.NoUnexpectedReceived();

            // 回復（null）で旗が戻り、同じ異常がまた出る。呼び手が別に Clear を書く必要は無い
            // Recovery (null) clears the flag so the same anomaly logs again; callers need no separate Clear
            gate.Observe(null);
            LogAssert.Expect(LogType.Error, "[Tag] b; suffix");
            gate.Observe("b");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ctorで受けたLogTypeで出す()
        {
            var gate = new AnomalyReportGate("", "", LogType.Warning);

            LogAssert.Expect(LogType.Warning, "w");
            gate.Observe("w");
            gate.Observe("w");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
