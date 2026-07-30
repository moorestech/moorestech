using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

public sealed class SchemaWatchTarget
{
    public readonly string WatchPath;
    public readonly string RequesterFolder;
    public readonly string RequesterFile;
    public readonly string ClassName;
    public readonly string AssemblyName;
    public readonly string WatchDescriptionJa;
    public readonly string WatchDescriptionEn;

    public SchemaWatchTarget(
        string watchPath,
        string requesterFolder,
        string requesterFile,
        string className,
        string assemblyName,
        string watchDescriptionJa,
        string watchDescriptionEn)
    {
        WatchPath = watchPath.Replace('\\', '/');
        RequesterFolder = requesterFolder;
        RequesterFile = requesterFile;
        ClassName = className;
        AssemblyName = assemblyName;
        WatchDescriptionJa = watchDescriptionJa;
        WatchDescriptionEn = watchDescriptionEn;
    }

    public bool TryReadCurrentHashes(out Dictionary<string, string> currentHashes)
    {
        currentHashes = new Dictionary<string, string>();
        if (!Directory.Exists(WatchPath))
        {
            Debug.LogWarning($"監視フォルダが見つかりません: {WatchPath}");
            return false;
        }

        // 相対pathとhashで差分検出
        // Detect changes by relative path and hash
        var files = Directory.GetFiles(WatchPath, "*.*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var relativePath = file.Substring(WatchPath.Length + 1).Replace('\\', '/');
            currentHashes[relativePath] = ComputeHash(file);
        }

        return true;
    }

    private static string ComputeHash(string filePath)
    {
        using var md5 = MD5.Create();
        var content = File.ReadAllBytes(filePath);
        var hash = md5.ComputeHash(content);
        return System.BitConverter.ToString(hash);
    }
}
