using System;
using System.IO;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 退避の1操作ぶんの結果。成功したかと、しなかった理由（ディスク失敗・退避元が無い/空）を分けて持ち帰る
    // One salvage operation's result: whether it succeeded and, if not, why (disk failure, or a missing/empty source)
    public sealed class SalvageOperationResult
    {
        public bool Succeeded;
        public string FailureReason;

        public static SalvageOperationResult Success()
        {
            return new SalvageOperationResult { Succeeded = true };
        }

        public static SalvageOperationResult Failure(string reason)
        {
            return new SalvageOperationResult { FailureReason = reason };
        }
    }

    // 退避のディスク操作。ディスクは他プロセスのロック・権限・空き容量に握られたプロセス外の資源なので境界として隔離する
    // The salvage's disk work; the disk is an out-of-process resource held by other processes' locks, permissions and free space, so it is isolated as a boundary
    // 隔離しないと起動パイプラインが Forget の下で死に、ローディング画面のまま永久に進まなくなる
    // Without the isolation the boot pipeline dies under Forget and the loading screen never advances
    public static class SalvageFileOperations
    {
        public static SalvageOperationResult CreateDirectory(string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);
                return SalvageOperationResult.Success();
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                return SalvageOperationResult.Failure($"ディレクトリを作れなかった: {e.Message}");
            }
        }

        // directory自体は残して中身だけ消す。並走プロセスの印を巻き込まないよう対象は呼び出し側が限定する
        // Empties the directory while keeping it; the caller narrows the target so a concurrent process's markers are never swept up
        public static SalvageOperationResult ClearDirectory(string directory)
        {
            if (directory == null || !Directory.Exists(directory)) return SalvageOperationResult.Success();
            // 中身の列挙・削除はディスクIO。並走プロセスのロックや権限不足で個々のDelete呼び出しが失敗しうる
            // Enumerating and deleting the contents is disk IO; a concurrent process's lock or missing permission can fail an individual Delete call
            try
            {
                foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
                foreach (var subDirectory in Directory.GetDirectories(directory)) Directory.Delete(subDirectory, true);
                return SalvageOperationResult.Success();
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                return SalvageOperationResult.Failure($"中身を消せなかった: {e.Message}");
            }
        }

        public static SalvageOperationResult DeleteDirectory(string directory)
        {
            if (directory == null || !Directory.Exists(directory)) return SalvageOperationResult.Success();
            // ディレクトリごとの再帰削除はディスクIO。子孫のどれかが他プロセスに掴まれていると失敗しうる
            // Recursively deleting the whole directory is disk IO; it can fail when any descendant is held open by another process
            try
            {
                Directory.Delete(directory, true);
                return SalvageOperationResult.Success();
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                return SalvageOperationResult.Failure($"ディレクトリを消せなかった: {e.Message}");
            }
        }

        public static SalvageOperationResult DeleteFile(string path)
        {
            if (path == null || !File.Exists(path)) return SalvageOperationResult.Success();
            // 単一ファイルの削除はディスクIO。ウイルススキャナ等の他プロセスに一時的にロックされて失敗しうる
            // Deleting a single file is disk IO; a transient lock from another process such as a virus scanner can fail it
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

        // ディレクトリごと改名して移す。ファイル単位で移すと空のサブディレクトリが元に残り、pid_*が無限に積み上がる
        // Renames the whole directory; moving file by file would leave empty subdirectories behind and let pid_* pile up forever
        public static SalvageOperationResult MoveDirectory(string source, string destination)
        {
            if (source == null || !Directory.Exists(source)) return SalvageOperationResult.Failure($"退避元が無い: {source}");
            // ディレクトリの改名移動はディスクIO。移動先が別ボリュームならDirectory.Moveはそもそも成立せずIOExceptionで失敗する
            // Renaming the directory is disk IO; across volumes Directory.Move cannot work at all and fails with IOException
            try
            {
                if (Directory.Exists(destination)) Directory.Delete(destination, true);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                Directory.Move(source, destination);
                return SalvageOperationResult.Success();
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                return SalvageOperationResult.Failure($"退避に失敗した: {e.Message}");
            }
        }

        // サブディレクトリ構造を保ったままファイルだけを移す。移動後は空になったサブディレクトリも畳む
        // Moves only the files while preserving the subdirectory structure, then folds the subdirectories left empty
        public static SalvageOperationResult MoveFilesInto(string source, string destination)
        {
            if (source == null || !Directory.Exists(source)) return SalvageOperationResult.Failure($"退避元が無い: {source}");
            // ファイル単位の移動と掃除はディスクIO。退避先の準備・個々のFile.Moveのどこで転んでも失敗しうる
            // Moving files one by one and folding empty subdirectories is disk IO; a failure can strike anywhere from preparing the destination to any single File.Move
            try
            {
                var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
                if (files.Length == 0) return SalvageOperationResult.Failure($"退避元が空: {source}");

                // 掃除の失敗を握ると前世代と今世代のスナップショットが混ざり、再現側は tick 番号だけでは世代を見分けられない
                // Swallowing a failed clean would mix two generations of snapshots, and the reproduction cannot tell them apart by tick number alone
                var clearing = ClearDirectory(destination);
                if (!clearing.Succeeded) return SalvageOperationResult.Failure($"退避先を空にできなかった: {clearing.FailureReason}");

                Directory.CreateDirectory(destination);
                foreach (var file in files)
                {
                    var destinationFile = Path.Combine(destination, Path.GetRelativePath(source, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
                    File.Move(file, destinationFile);
                }
                foreach (var subDirectory in Directory.GetDirectories(source)) Directory.Delete(subDirectory, true);
                return SalvageOperationResult.Success();
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                return SalvageOperationResult.Failure($"退避に失敗した: {e.Message}");
            }
        }

        // 走査自体が失敗したら「中身なし」ではなく理由付きで返す。無音で空扱いにすると退避物の消失に気づけない
        // A failed scan comes back with its reason instead of reading as empty, so a lost salvage never goes unnoticed
        public static SalvageOperationResult ProbeHasAnyFile(string directory)
        {
            if (directory == null || !Directory.Exists(directory)) return SalvageOperationResult.Failure($"退避先が無い: {directory}");
            // 退避先の走査はディスクIO。権限不足やロックで列挙自体が失敗しうる
            // Scanning the salvage destination is disk IO; enumeration itself can fail from missing permission or a lock
            try
            {
                if (Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length == 0) return SalvageOperationResult.Failure($"退避先が空: {directory}");
                return SalvageOperationResult.Success();
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                return SalvageOperationResult.Failure($"退避先を読めなかった: {e.Message}");
            }
        }
    }
}
