namespace Client.Game.InGame.Playtest.Progress
{
    // 購読では取れない「操作そのもの」を記録側へプッシュする窓口。実装は ProgressRecorder 1つ
    // The window that pushes actions themselves, which no subscription observes; ProgressRecorder is the only implementation
    public interface IPlaytestProgressSink
    {
        void RecordReportSent(string kind);
    }
}
