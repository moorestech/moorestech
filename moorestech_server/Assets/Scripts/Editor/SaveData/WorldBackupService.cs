using System;
using System.IO;
using Game.Paths;

/// <summary>既定ワールドディレクトリの複製・削除を担うIO層。UIを一切持たない</summary>
/// <summary>IO layer that copies and deletes the default world directory; holds no UI</summary>
internal static class WorldBackupService
{
    // 中断時の残骸と正規の成果物を区別するための一時サフィックス
    // Temp suffix that tells an interrupted remnant apart from a finished artifact
    private const string IncompleteSuffix = ".incomplete";

    internal static bool WorldExists()
    {
        return Directory.Exists(GameSystemPaths.DefaultWorldDirectory);
    }

    // 不正な説明はnullで拒否する
    // Rejects an invalid description with null
    internal static string BuildBackupFolderName(string dateString, string description)
    {
        if (string.IsNullOrEmpty(description))
            return $"Backup_{dateString}";

        if (!IsValidDescription(description))
            return null;

        return $"Backup_{dateString}_{description}";

        #region Internal

        bool IsValidDescription(string value)
        {
            if (value.Contains('/') || value.Contains('\\') || value.Contains(".."))
                return false;
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
                if (value.Contains(invalidChar))
                    return false;
            return true;
        }

        #endregion
    }

    // 宛先ガード・コピー・（成功時のみの）削除を1手順に畳む
    // Folds the destination guard, copy, and delete-on-success into one step
    internal static bool TryBackupWorld(string backupFolderName, bool deleteWorldAfterBackup, out string backupPath, out string denyReason)
    {
        backupPath = null;
        denyReason = null;

        if (BackupDestinationExists(backupFolderName))
        {
            denyReason = $"Backup destination already exists.\n\n{GetBackupWorldPath(backupFolderName)}";
            return false;
        }

        backupPath = BackupWorld(backupFolderName);

        if (deleteWorldAfterBackup)
            DeleteWorld();

        return true;
    }

    internal static void DeleteWorld()
    {
        // 削除前に退避し、途中失敗しても正規名は壊れた状態にしない
        // Moves aside before deleting so a mid-failure never corrupts the canonical name
        var source = GameSystemPaths.DefaultWorldDirectory;
        var pendingDeletion = source + IncompleteSuffix;
        DiscardRemnant(pendingDeletion);
        Directory.Move(source, pendingDeletion);
        Directory.Delete(pendingDeletion, true);
    }

    internal static long GetWorldSizeBytes()
    {
        var total = 0L;
        foreach (var file in Directory.GetFiles(GameSystemPaths.DefaultWorldDirectory, "*", SearchOption.AllDirectories))
            total += new FileInfo(file).Length;
        return total;
    }

    // 前回の中断で残った一時ディレクトリを捨てて再入を通す
    // Drops a temp directory left by an earlier interruption so the retry can proceed
    private static void DiscardRemnant(string incompletePath)
    {
        if (Directory.Exists(incompletePath)) Directory.Delete(incompletePath, true);
    }

    private static bool BackupDestinationExists(string backupFolderName)
    {
        return Directory.Exists(GetBackupWorldPath(backupFolderName));
    }

    // world_1をterrain/cache含め丸ごと複製する
    // Copies world_1 wholesale, including terrain and cache
    private static string BackupWorld(string backupFolderName)
    {
        var destination = GetBackupWorldPath(backupFolderName);
        var incompleteDestination = destination + IncompleteSuffix;

        // 一時名へ複製し、完了後にDirectory.Moveで正規名へ確定する
        // Copies to a temp name, then commits it to the canonical name via Directory.Move
        DiscardRemnant(incompleteDestination);
        CopyDirectory(GameSystemPaths.DefaultWorldDirectory, incompleteDestination);
        Directory.Move(incompleteDestination, destination);
        return destination;

        #region Internal

        void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);

            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));

            foreach (var directory in Directory.GetDirectories(source))
                CopyDirectory(directory, Path.Combine(dest, Path.GetFileName(directory)));
        }

        #endregion
    }

    // Saves外への逸脱を入口で拒否する
    // Rejects a path that escapes the save directory
    private static string GetBackupWorldPath(string backupFolderName)
    {
        var candidate = Path.GetFullPath(Path.Combine(GameSystemPaths.SaveFileDirectory, backupFolderName, GameSystemPaths.DefaultWorldName));
        var saveRootPrefix = Path.GetFullPath(GameSystemPaths.SaveFileDirectory) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(saveRootPrefix, StringComparison.Ordinal))
            throw new ArgumentException("Backup folder name resolved outside the save directory.", nameof(backupFolderName));
        return candidate;
    }
}
