using System.IO;
using Game.Paths;

/// <summary>既定ワールドディレクトリの複製・削除を担うIO層。UIを一切持たない</summary>
/// <summary>IO layer that copies and deletes the default world directory; holds no UI</summary>
public static class WorldBackupService
{
    public static string WorldDirectory => GameSystemPaths.DefaultWorldDirectory;
    public static string WorldName => GameSystemPaths.DefaultWorldName;

    public static bool WorldExists()
    {
        return Directory.Exists(WorldDirectory);
    }

    public static bool BackupDestinationExists(string backupFolderName)
    {
        return Directory.Exists(GetBackupWorldPath(backupFolderName));
    }

    // Saves/<backupFolderName>/world_1 へワールドを丸ごと複製する。terrainやcacheも除外しない
    // Copies the whole world into Saves/<backupFolderName>/world_1, excluding nothing
    public static string BackupWorld(string backupFolderName)
    {
        var destination = GetBackupWorldPath(backupFolderName);
        CopyDirectory(WorldDirectory, destination);
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

    public static void DeleteWorld()
    {
        Directory.Delete(WorldDirectory, true);
    }

    public static long GetWorldSizeBytes()
    {
        var total = 0L;
        foreach (var file in Directory.GetFiles(WorldDirectory, "*", SearchOption.AllDirectories))
            total += new FileInfo(file).Length;
        return total;
    }

    public static string GetBackupWorldPath(string backupFolderName)
    {
        return Path.Combine(GameSystemPaths.SaveFileDirectory, backupFolderName, WorldName);
    }
}
