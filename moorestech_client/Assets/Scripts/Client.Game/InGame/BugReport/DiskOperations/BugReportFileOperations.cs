using System;
using System.IO;

namespace Client.Game.InGame.BugReport.DiskOperations
{
    // バグ報告・退避・進行記録が共有するファイル単位のディスク操作。ディレクトリ単位は BugReportDiskOperations が持つ
    // File-level disk work shared by bug reports, salvage and progress records; directory-level work lives in BugReportDiskOperations
    // 隔離しないと終了時の書き出し例外が GameShutdownEvent の参加者ループを止め、セーブもされないまま終了もしない
    // Without the isolation a write failure at shutdown stops the participant loop in GameShutdownEvent, leaving the game neither saved nor closed
    public static class BugReportFileOperations
    {
        // 箱の置き場作りも終了パイプラインの中で走る。mkdirだけ素のままだと満杯・権限で参加者ループごと止まる
        // Creating the box also runs inside the shutdown pipeline; a bare mkdir would let a full or read-only disk stop the participant loop
        public static SalvageOperationResult CreateBundleDirectory(string rootDirectory, out string directory)
        {
            directory = null;
            try
            {
                directory = BugReportOutbox.CreateBundleDirectory(rootDirectory, DateTime.UtcNow, BugReportOutbox.CreateShortId());
                return SalvageOperationResult.Success();
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                return SalvageOperationResult.Failure($"箱の置き場を作れなかった {rootDirectory}: {e.Message}");
            }
        }

        public static SalvageOperationResult DeleteFile(string path)
        {
            if (path == null || !File.Exists(path)) return SalvageOperationResult.Success();
            try
            {
                File.Delete(path);
                return SalvageOperationResult.Success();
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                return SalvageOperationResult.Failure($"ファイルを消せなかった: {e.Message}");
            }
        }

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

        // ファイルが無いのは0行の成功。読めなかったときだけ失敗を返し、空と取り違えさせない
        // A missing file is a zero-line success; only an unreadable one fails, so it is never mistaken for empty
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
    }
}
