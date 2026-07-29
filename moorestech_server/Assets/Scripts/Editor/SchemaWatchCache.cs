using System.Collections.Generic;
using System.IO;

public sealed class SchemaWatchCache
{
    private readonly string cacheFilePath;
    private readonly Dictionary<string, Dictionary<string, string>> hashesByWatchPath =
        new Dictionary<string, Dictionary<string, string>>();

    public SchemaWatchCache(string cacheFilePath)
    {
        this.cacheFilePath = cacheFilePath;
        Load();
    }

    public bool HasFolderChanged(
        SchemaWatchTarget target,
        Dictionary<string, string> currentHashes)
    {
        if (!hashesByWatchPath.TryGetValue(target.WatchPath, out var cachedHashes))
        {
            return true;
        }

        if (cachedHashes.Count != currentHashes.Count)
        {
            return true;
        }

        // パスとハッシュを照合し、同数のファイル差し替えも検出する。
        // Compare paths and hashes to catch replacements with unchanged file counts.
        foreach (var currentHash in currentHashes)
        {
            if (!cachedHashes.TryGetValue(currentHash.Key, out var cachedHash) ||
                cachedHash != currentHash.Value)
            {
                return true;
            }
        }

        return false;
    }

    public void ReplaceHashes(
        SchemaWatchTarget target,
        Dictionary<string, string> currentHashes)
    {
        hashesByWatchPath[target.WatchPath] =
            new Dictionary<string, string>(currentHashes);
    }

    public void Save()
    {
        var lines = new List<string>();
        foreach (var targetHashes in hashesByWatchPath)
        {
            foreach (var fileHash in targetHashes.Value)
            {
                lines.Add($"{targetHashes.Key}|{fileHash.Key}|{fileHash.Value}");
            }
        }

        // 安定した順序で全対象を書き出し、対象別状態の欠落を防ぐ。
        // Write every target in stable order to avoid losing target-specific state.
        lines.Sort();
        File.WriteAllLines(cacheFilePath, lines.ToArray());
    }

    private void Load()
    {
        if (!File.Exists(cacheFilePath))
        {
            return;
        }

        // 旧2列形式は安全に無視し、次回検査で全対象を初期同期する。
        // Ignore the legacy two-column format and initialize all targets on the next check.
        var lines = File.ReadAllLines(cacheFilePath);
        foreach (var line in lines)
        {
            var parts = line.Split('|');
            if (parts.Length != 3)
            {
                continue;
            }

            if (!hashesByWatchPath.TryGetValue(parts[0], out var targetHashes))
            {
                targetHashes = new Dictionary<string, string>();
                hashesByWatchPath[parts[0]] = targetHashes;
            }

            targetHashes[parts[1]] = parts[2];
        }
    }
}
