using Client.Ui.Foundation.Stage.ScaleReading;
using NUnit.Framework;

namespace Client.Tests.Ui.Stage
{
    /// <summary>
    /// 倍率の異常は入口で 1 回、回復で旗が戻る
    /// Scale anomaly once on entry; recovery clears flag
    /// </summary>
    public class StageScaleReadGuardTest
    {
        [Test]
        public void 読めない間は1回だけ報告し_回復後に読めなくなるとまた報告する()
        {
            var guard = new StageScaleReadGuard();

            Assert.IsFalse(guard.ShouldReportUnreadable(true));
            Assert.IsTrue(guard.ShouldReportUnreadable(false));
            Assert.IsFalse(guard.ShouldReportUnreadable(false));

            // 回復で旗が戻る
            // Recovery clears the flag
            Assert.IsFalse(guard.ShouldReportUnreadable(true));
            Assert.IsTrue(guard.ShouldReportUnreadable(false));
        }
    }
}
