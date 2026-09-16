using System;
using System.Collections.Generic;
using System.IO;

namespace Client.Game.InGame.BugReport.DiskOperations
{
    // バグ報告・退避・進行記録が共有するディレクトリ単位のディスク操作。ファイル単位は BugReportFileOperations が持つ
    // Directory-level disk work shared by bug reports, salvage and progress records; file-level work lives in BugReportFileOperations
    // ディスクは他プロセスのロック・権限・空き容量に握られたプロセス外の資源なので境界として隔離する。隔離しないと起動・終了パイプラインが止まる
    // The disk is an out-of-process resource held by other processes' locks, permissions and free space, so it is isolated; otherwise boot and shutdown pipelines stall
    public static class BugReportDiskOperations
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

        // 中身が残っていれば消さずに成功を返す。セッションの段を畳んだ後の空のpid_<PID>だけを片付ける用途
        // Returns success without deleting when anything remains; meant for clearing a pid_<PID> left empty after its sessions were folded
        public static SalvageOperationResult DeleteDirectoryIfEmpty(string directory)
        {
            if (directory == null || !Directory.Exists(directory)) return SalvageOperationResult.Success();
            try
            {
                if (Directory.GetFileSystemEntries(directory).Length == 0) Directory.Delete(directory, false);
                return SalvageOperationResult.Success();
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                return SalvageOperationResult.Failure($"空のディレクトリを消せなかった: {e.Message}");
            }
        }

        // ディレクトリごと改名して移す。ファイル単位で移すと空のサブディレクトリが元に残り、pid_*が無限に積み上がる
        // Renames the whole directory; moving file by file would leave empty subdirectories behind and let pid_* pile up forever
        public static SalvageOperationResult MoveDirectory(string source, string destination)
        {
            if (source == null || !Directory.Exists(source)) return SalvageOperationResult.Failure($"移動元が無い: {source}");
            try
            {
                if (Directory.Exists(destination)) Directory.Delete(destination, true);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                Directory.Move(source, destination);
                return SalvageOperationResult.Success();
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                return SalvageOperationResult.Failure($"移動に失敗した: {e.Message}");
            }
        }

        // pid_<PID>/session_<utcTicks>/ 等の入れ子を保ったままファイルを移す唯一の実装。移した相対パスを返し、元の空サブディレクトリは畳む
        // The only implementation moving files while keeping nesting such as pid_<PID>/session_<utcTicks>/; returns the relative paths moved and folds the emptied subdirectories
        // 移動元が無ければ0件の成功。途中で転んだら、それまでに移した分を moved に残したまま失敗を返す
        // A missing source is a zero-file success; a failure midway returns with whatever was already moved kept in moved
        public static SalvageOperationResult MoveTree(string source, string destination, out List<string> moved)
        {
            moved = new List<string>();
            if (source == null || !Directory.Exists(source)) return SalvageOperationResult.Success();
            try
            {
                foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(source, file);
                    var destinationFile = Path.Combine(destination, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
                    File.Move(file, destinationFile);
                    moved.Add(relativePath);
                }
                foreach (var subDirectory in Directory.GetDirectories(source)) Directory.Delete(subDirectory, true);
                return SalvageOperationResult.Success();
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                return SalvageOperationResult.Failure($"移動に失敗した（{moved.Count}件は移動済み）: {e.Message}");
            }
        }

        // 移動先を空にしてから入れ子ごと移す。空の移動元で移動先を消さないよう、中身の確認を先に行う
        // Empties the destination and then moves the tree; the source is checked first so an empty source never wipes the destination
        public static SalvageOperationResult MoveFilesInto(string source, string destination)
        {
            var sourceProbe = ProbeHasAnyFile(source);
            if (!sourceProbe.Succeeded) return SalvageOperationResult.Failure($"退避元を移せない: {sourceProbe.FailureReason}");

            // 掃除の失敗を握ると前世代と今世代のスナップショットが混ざり、再現側は tick 番号だけでは世代を見分けられない
            // Swallowing a failed clean would mix two generations of snapshots, and the reproduction cannot tell them apart by tick number alone
            var clearing = ClearDirectory(destination);
            if (!clearing.Succeeded) return SalvageOperationResult.Failure($"退避先を空にできなかった: {clearing.FailureReason}");
            return MoveTree(source, destination, out _);
        }

        // 走査自体が失敗したら「中身なし」ではなく理由付きで返す。無音で空扱いにすると退避物の消失に気づけない
        // A failed scan comes back with its reason instead of reading as empty, so a lost salvage never goes unnoticed
        public static SalvageOperationResult ProbeHasAnyFile(string directory)
        {
            if (directory == null || !Directory.Exists(directory)) return SalvageOperationResult.Failure($"ディレクトリが無い: {directory}");
            try
            {
                if (Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length == 0) return SalvageOperationResult.Failure($"ディレクトリが空: {directory}");
                return SalvageOperationResult.Success();
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                return SalvageOperationResult.Failure($"ディレクトリを読めなかった: {e.Message}");
            }
        }
    }
}
