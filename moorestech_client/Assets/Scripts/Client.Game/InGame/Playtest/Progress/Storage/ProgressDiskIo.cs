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
        // 箱の置き場作りも終了パイプラインの中で走る。mkdirだけ素のままだと満杯・権限で参加者ループごと止まり、セーブも終了も起きない
        // Creating the box also runs inside the shutdown pipeline; a bare mkdir would let a full or read-only disk stop the participant loop, saving and quitting with it
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

        public static SalvageOperationResult WriteText(string path, string text)
        {
            // 内容の書き出しはディスクIO。容量不足や権限で失敗しても終了パイプラインは止めない
            // Writing the content is disk IO; running out of space or lacking permission must not stop the shutdown pipeline
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
            // 追記口を開くopenはディスクIO。他プロセスに掴まれている等で失敗しうる
            // Opening the append handle is disk IO; it can fail when the file is held by another process, among other causes
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
            // 1行ごとのwrite/flushはディスクIO。書き込み最中に容量不足で失敗しうる
            // The per-line write and flush are disk IO; running out of space mid-write can fail them
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
            // ハンドルのdisposeはディスクIO。バッファの最終flushが容量不足やロックで失敗しうる
            // Disposing the handle is disk IO; the final buffered flush can fail from a full disk or a lock
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
            // 全文読み込みはディスクIO。読んでいる最中に他プロセスに削除・ロックされて失敗しうる
            // Reading the whole file is disk IO; another process deleting or locking it mid-read can fail this
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
            // 全行読み込みもディスクIO。ReadTextと同じく読み取り中の削除・ロックで失敗しうる
            // Reading all lines is disk IO too; like ReadText it can fail from a delete or lock mid-read
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
