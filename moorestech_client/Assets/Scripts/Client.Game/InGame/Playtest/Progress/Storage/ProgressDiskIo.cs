using System;
using System.IO;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.LastSession;

namespace Client.Game.InGame.Playtest.Progress
{
    // 進行記録のディスク操作。ディスクは他プロセスのロック・権限・空き容量に握られたプロセス外の資源なので境界として隔離する
    // The progress record's disk work; the disk is an out-of-process resource held by other processes' locks, permissions and free space, so it is isolated as a boundary
    // 隔離しないと終了時の書き出し例外が GameShutdownEvent の参加者ループを止め、セーブもされないまま終了もしない
    // Without the isolation a write failure at shutdown stops the participant loop in GameShutdownEvent, leaving the game neither saved nor closed
    public static class ProgressDiskIo
    {
        public static SalvageOperationResult WriteText(string path, string text)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, text);
                return SalvageOperationResult.Success();
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                return SalvageOperationResult.Failure($"書き出しに失敗した {path}: {e.Message}");
            }
        }

        // 追記口は1セッションに1本だけ開く。行ごとに開き直すとメインスレッドで mkdir/open/close が設置のたびに走る
        // Exactly one appender per session; reopening per line would run mkdir/open/close on the main thread for every placement
        public static SalvageOperationResult OpenAppender(string path, out StreamWriter appender)
        {
            appender = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                appender = new StreamWriter(path, true);
                return SalvageOperationResult.Success();
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                return SalvageOperationResult.Failure($"追記口を開けなかった {path}: {e.Message}");
            }
        }

        // 1行ごとに Flush する。クラッシュで失うのは書きかけの1行だけに留める
        // Flushes per line so a crash loses at most the one line being written
        public static SalvageOperationResult AppendLine(StreamWriter appender, string line)
        {
            try
            {
                appender.WriteLine(line);
                appender.Flush();
                return SalvageOperationResult.Success();
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                return SalvageOperationResult.Failure($"追記に失敗した: {e.Message}");
            }
        }

        public static SalvageOperationResult CloseAppender(StreamWriter appender)
        {
            try
            {
                appender.Dispose();
                return SalvageOperationResult.Success();
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                return SalvageOperationResult.Failure($"追記口を閉じられなかった: {e.Message}");
            }
        }

        public static SalvageOperationResult ReadText(string path, out string text)
        {
            text = null;
            if (!File.Exists(path)) return SalvageOperationResult.Failure($"ファイルが無い: {path}");
            try
            {
                text = File.ReadAllText(path);
                return SalvageOperationResult.Success();
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                return SalvageOperationResult.Failure($"読み込みに失敗した {path}: {e.Message}");
            }
        }

        public static SalvageOperationResult ReadLines(string path, out string[] lines)
        {
            lines = Array.Empty<string>();
            if (!File.Exists(path)) return SalvageOperationResult.Success();
            try
            {
                lines = File.ReadAllLines(path);
                return SalvageOperationResult.Success();
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                return SalvageOperationResult.Failure($"読み込みに失敗した {path}: {e.Message}");
            }
        }
    }
}
