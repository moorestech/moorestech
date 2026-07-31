using System.IO;
using System.Linq;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// ローカルサーバーが読むゲームデータ一式を成果物ルートのgame/へ同梱する
/// Bundles the game data the local server reads into game/ at the artifact root
/// </summary>
public static class GameDataBundler
{
    public static void Bundle(string outputDirectory, bool isStrict)
    {
        // 正本は隣接リポジトリの ../moorestech_master/server_v8
        // The source of truth is ../moorestech_master/server_v8 beside this repository
        var sourceDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "moorestech_master", "server_v8"));

        // 必須構成（config/map/mods）が欠けた成果物を出さない
        // Never ship an artifact missing the required config/map/mods layout
        var missingPath = FindMissingRequiredPath(sourceDirectory);
        if (missingPath != string.Empty)
        {
            if (isStrict) throw new BuildFailedException($"[GameDataBundler] required game data is missing: {missingPath}");
            Debug.LogWarning($"[GameDataBundler] required game data is missing: {missingPath}");
            return;
        }

        var destinationDirectory = Path.Combine(outputDirectory, "game");
        if (Directory.Exists(destinationDirectory)) Directory.Delete(destinationDirectory, true);

        // OS由来のゴミを除いて全体をコピーする
        // Copy the whole tree, excluding OS junk files
        var copiedFileCount = 0;
        foreach (var sourceFile in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(sourceFile) == ".DS_Store") continue;
            var relativePath = sourceFile.Substring(sourceDirectory.Length + 1);
            var destinationFile = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
            File.Copy(sourceFile, destinationFile, true);
            copiedFileCount++;
        }

        Debug.Log($"[GameDataBundler] bundled game data: {copiedFileCount} files at {destinationDirectory}");
    }

    private static string FindMissingRequiredPath(string sourceDirectory)
    {
        if (!Directory.Exists(sourceDirectory)) return sourceDirectory;

        var mapJson = Path.Combine(sourceDirectory, "map", "map.json");
        if (!File.Exists(mapJson)) return mapJson;

        var modsDirectory = Path.Combine(sourceDirectory, "mods");
        if (!Directory.Exists(modsDirectory)) return modsDirectory;

        // ローカライズはmod直下のlocalization/localization.csvが正（旧config/は廃止済み）
        // Localization lives at localization/localization.csv inside each mod (legacy config/ is gone)
        var hasModLocalization = Directory
            .GetDirectories(modsDirectory)
            .Any(modDirectory => File.Exists(Path.Combine(modDirectory, "localization", "localization.csv")));
        if (!hasModLocalization) return Path.Combine(modsDirectory, "*", "localization", "localization.csv");

        return string.Empty;
    }
}
