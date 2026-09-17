using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Recording;
using Client.Game.InGame.BugReport.Submit;
using Client.Game.InGame.Playtest.Progress;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration
{
    /// <summary>
    /// バグ報告の確保と進行記録の登録。集めない起動（リモート接続・WebUI無し）では記録を取る側を一切登録しない。
    /// Registers bug-report capture and the progress record; boots that collect nothing (remote connection, no web UI) register none of the recording side.
    /// </summary>
    internal static class PlaytestRecordRegistration
    {
        public static void Register(ContainerBuilder builder, bool collectsPlaytestRecords)
        {
            // テスター識別の差し替え点は PlaytestSessionIdentityProvider 1つ。DI確立前に走る開始ゲートも同じ値を読む（ADR 0060 裁定4）
            // PlaytestSessionIdentityProvider is the single seam for the tester identity; the start gates, which run before DI exists, read the same value (ADR 0060 adjudication 4)
            builder.Register<IPlaytestSessionIdentity>(_ => PlaytestSessionIdentityProvider.Current, Lifetime.Singleton);

            // ポーズメニューのtopicと報告actionは確保セッション・書き出し口・進行記録の窓口を必ず受け取るので、どちらの起動でも登録する
            // The pause-menu topic and the report action always take the capture session, writer and progress window, so both boots register them
            builder.Register<BugReportBundleWriter>(Lifetime.Singleton);
            builder.Register<BugReportCaptureSession>(Lifetime.Singleton);
            builder.Register<BugReportSubmitter>(Lifetime.Singleton);

            if (!collectsPlaytestRecords)
            {
                // 確保の起点（ポーズメニューの契機）を登録しないので確保は始まらず、状態は確保なしのまま送信は拒否される
                // With no capture trigger registered no capture ever begins, the status stays no-session and a send is refused
                builder.Register<IBugReportCaptureSources, UncollectedBugReportCaptureSources>(Lifetime.Singleton);
                builder.Register<IPlaytestProgressSink, UncollectedPlaytestProgressSink>(Lifetime.Singleton);
                return;
            }

            // バグ報告の常時記録（ログリング・録画リング）と確保の起点
            // Always-on capture for bug reports (log ring, frame recording ring) and the capture triggers
            builder.RegisterEntryPoint<UnityLogRing>().AsSelf();
            builder.RegisterEntryPoint<GameFrameRecorder>().AsSelf();
            builder.Register<IBugReportCaptureSources, BugReportCaptureSources>(Lifetime.Singleton);
            builder.RegisterEntryPoint<BugReportCaptureEventHandler>();
            builder.RegisterEntryPoint<BugReportUiStatePusher>();
            builder.RegisterEntryPoint<BugReportPauseMenuTrigger>();

            // 進行記録は購読で集める。UIStateControl はシーン上のcomponentとして既存の登録から解決される
            // The progress record collects through subscriptions; UIStateControl resolves from the existing scene component registration
            builder.RegisterEntryPoint<ProgressRecorder>().AsSelf().As<IPlaytestProgressSink>();
        }
    }
}
