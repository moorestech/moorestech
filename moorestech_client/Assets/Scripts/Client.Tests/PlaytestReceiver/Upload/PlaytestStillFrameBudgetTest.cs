using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Recording;
using Client.PlaytestReceiver;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestStillFrameBudgetTest
    {
        // 録画の最長（保持秒＋最古区間と書き込み中区間の2区間）ぶんの静止画が、箱の件数上限の半分に収まる（D1）
        // Stills for the longest recording (retention plus the oldest and in-flight segments) fit in half of the box's file cap (D1)
        [Test]
        public void 最長の録画でも静止画は件数上限の半分に収まる()
        {
            var longestSeconds = GameFrameRecorder.RetentionSeconds + 2 * GameFrameRecorder.SegmentSeconds;
            var maxStills = longestSeconds / BugReportBundleWriter.StillFrameIntervalSeconds + 1;
            Assert.LessOrEqual(maxStills, PlaytestReceiverConfig.MaxBundleFiles / 2);
        }
    }
}
