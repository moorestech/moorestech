using System.Collections.Generic;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.Playtest.Progress;

namespace Client.Tests.Playtest
{
    // プッシュされた内容だけを覚えるテスト用の記録先
    // A test sink that only remembers what was pushed
    public sealed class RecordingProgressSink : IPlaytestProgressSink
    {
        public readonly List<PlaytestReportKind> SentReportKinds = new();

        public void RecordReportSent(PlaytestReportKind kind)
        {
            SentReportKinds.Add(kind);
        }
    }
}
