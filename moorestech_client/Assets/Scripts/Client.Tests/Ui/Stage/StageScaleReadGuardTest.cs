using Client.Ui.Foundation.Stage.ScaleReading;
using NUnit.Framework;

namespace Client.Tests.Ui.Stage
{
    /// <summary>
    /// 倍率が読めない異常は入ったとき 1 回、回復で旗が戻る（L0。StageScaleWatch と SlotFrameRing が共有）
    /// An unreadable scale is reported once on entry and recovery clears the flag (L0; shared by StageScaleWatch and SlotFrameRing)
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
