using System;
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

        void Load()
        {
            if (!File.Exists(cacheFilePath))
            {
                return;
            }

            var lines = File.ReadAllLines(cacheFilePath);
            if (0 < lines.Length && lines[0] == "V|2")
            {
                // version 2は空対象と区切り文字を含むパスを明示的に復元する。
                // Version 2 restores empty targets and paths containing delimiters explicitly.
                for (var index = 1; index < lines.Length; index++)
                {
                    LoadVersionTwoLine(lines[index]);
                }

                return;
            }

            // 既存3列形式だけを移行し、旧2列形式は初回同期へ委ねる。
            // Migrate only the existing three-column format and leave legacy two-column data to initial sync.
            foreach (var legacyLine in lines)
            {
                var legacyParts = legacyLine.Split('|');
                if (legacyParts.Length == 3)
                {
                    EnsureTargetHashes(legacyParts[0])[legacyParts[1]] = legacyParts[2];
                }
            }

            #region Internal

            void LoadVersionTwoLine(string line)
            {
                var parts = line.Split('|');
                if (parts.Length == 2 && parts[0] == "T")
                {
                    EnsureTargetHashes(Uri.UnescapeDataString(parts[1]));
                    return;
                }

                if (parts.Length == 4 && parts[0] == "F")
                {
                    var watchPath = Uri.UnescapeDataString(parts[1]);
                    var relativePath = Uri.UnescapeDataString(parts[2]);
                    EnsureTargetHashes(watchPath)[relativePath] = parts[3];
                }
            }

            Dictionary<string, string> EnsureTargetHashes(string watchPath)
            {
                if (!hashesByWatchPath.TryGetValue(watchPath, out var targetHashes))
                {
                    targetHashes = new Dictionary<string, string>();
                    hashesByWatchPath[watchPath] = targetHashes;
                }

                return targetHashes;
            }

            #endregion
        }
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
            lines.Add($"T|{Escape(targetHashes.Key)}");
            foreach (var fileHash in targetHashes.Value)
            {
                lines.Add($"F|{Escape(targetHashes.Key)}|{Escape(fileHash.Key)}|{fileHash.Value}");
            }
        }

        // 安定した順序で全対象を書き出し、対象別状態の欠落を防ぐ。
        // Write every target in stable order to avoid losing target-specific state.
        lines.Sort();
        lines.Insert(0, "V|2");
        File.WriteAllLines(cacheFilePath, lines.ToArray());

        #region Internal

        string Escape(string value)
        {
            return Uri.EscapeDataString(value);
        }

        #endregion
    }
}
