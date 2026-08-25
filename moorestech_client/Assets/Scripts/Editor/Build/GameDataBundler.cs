using System.Collections.Generic;
using System.IO;
using UnityEditor.Build;
using UnityEngine;

namespace Client.Editor.Build
{
    /// <summary>
    /// ローカルサーバーが読むゲームデータ一式を成果物ルートのgame/へ同梱する
    /// Bundles the game data the local server reads into game/ at the artifact root
    /// </summary>
    public static class GameDataBundler
    {
        // OS由来のゴミはサーバーが読まないので同梱しない
        // OS junk files are never read by the server, so they do not ship
        private static readonly IReadOnlyList<string> ExcludedFileNames = new[] { ".DS_Store" };

        public static void Bundle(string outputDirectory, bool isStrict)
        {
            // 正本は隣接リポジトリの ../moorestech_master/server_v8
            // The source of truth is ../moorestech_master/server_v8 beside this repository
            var sourceDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "moorestech_master", "server_v8"));

            // 必須構成（map/mods）が欠けた成果物を出さない
            // Never ship an artifact missing the required map/mods layout
            var missingPath = FindMissingRequiredPath();
            if (missingPath != string.Empty)
            {
                if (isStrict) throw new BuildFailedException($"[GameDataBundler] required game data is missing: {missingPath}");
                Debug.LogWarning($"[GameDataBundler] required game data is missing: {missingPath}");
                return;
            }

            var destinationDirectory = Path.Combine(outputDirectory, "game");
            var copiedFileCount = DirectoryProcessor.CopyAndReplace(sourceDirectory, destinationDirectory, ExcludedFileNames);
            Debug.Log($"[GameDataBundler] bundled game data: {copiedFileCount} files at {destinationDirectory}");

            #region Internal

            string FindMissingRequiredPath()
            {
                if (!Directory.Exists(sourceDirectory)) return sourceDirectory;

                var mapJson = Path.Combine(sourceDirectory, "map", "map.json");
                if (!File.Exists(mapJson)) return mapJson;

                var modsDirectory = Path.Combine(sourceDirectory, "mods");
                if (!Directory.Exists(modsDirectory)) return modsDirectory;

                // ランタイムはmodごとのlocalization/localization.csvをマージするので1件も無ければ欠損
                // At runtime each mod's localization/localization.csv is merged, so zero files means missing
                var modDirectories = Directory.GetDirectories(modsDirectory);
                var anyLocalizationCsvPath = Path.Combine(modsDirectory, "*", "localization", "localization.csv");
                foreach (var modDirectory in modDirectories)
                {
                    if (File.Exists(Path.Combine(modDirectory, "localization", "localization.csv"))) return string.Empty;
                }

                return anyLocalizationCsvPath;
            }

            #endregion
        }
    }
}
