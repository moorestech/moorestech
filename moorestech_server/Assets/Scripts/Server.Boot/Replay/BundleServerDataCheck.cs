using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Server.Boot.Replay
{
    // 渡されたサーバーデータが記録時のものかを箱の manifest と突き合わせる。食い違いはマスタローダーの不可解な例外になる
    // Checks the given server data against the bundle's manifest; a mismatch surfaces as an inscrutable master-loader exception
    public static class BundleServerDataCheck
    {
        private const string ManifestFileName = "manifest.json";
        private const string ModsDirectoryName = "mods";

        // 食い違いの理由を返す。問題なければ null
        // Returns the reason for a mismatch, or null when there is none
        public static string FindMismatch(string bundleDirectory, string serverDataDirectory)
        {
            if (string.IsNullOrEmpty(serverDataDirectory)) return $"サーバーデータディレクトリが渡されていません。記録時のマスタが分からないまま再生はできません bundle:{bundleDirectory}";
            if (!Directory.Exists(serverDataDirectory)) return $"渡されたサーバーデータがありません given:{serverDataDirectory}";
            if (!Directory.Exists(Path.Combine(serverDataDirectory, ModsDirectoryName))) return $"渡されたサーバーデータに {ModsDirectoryName}/ がありません given:{serverDataDirectory}";

            var recorded = ReadRecordedServerData(bundleDirectory);
            if (recorded == null)
            {
                Debug.LogWarning($"バグ報告バンドルの manifest に serverData がないため、渡されたサーバーデータを突き合わせられません given:{serverDataDirectory} bundle:{bundleDirectory}");
                return null;
            }

            var given = Normalize(serverDataDirectory);
            var relativePath = (string)recorded["relativePath"] ?? "";
            var recordedPath = (string)recorded["path"] ?? "";
            if (relativePath.Length == 0)
            {
                if (string.Equals(given, Normalize(recordedPath), StringComparison.OrdinalIgnoreCase)) return null;
                return $"記録時のサーバーデータがリポジトリの外にあり受け側で解決できません 期待:{recordedPath} 渡された:{serverDataDirectory}";
            }

            if (given.EndsWith("/" + relativePath, StringComparison.OrdinalIgnoreCase)) return null;
            return $"渡されたサーバーデータが記録時のものと違います 期待:<{(string)recorded["relativeTo"]}>/{relativePath}（記録時 {recordedPath}） 渡された:{serverDataDirectory}";
        }

        private static string Normalize(string path)
        {
            return path.Replace(Path.DirectorySeparatorChar, '/').TrimEnd('/');
        }

        // manifest は別マシンで作られた外部入力。壊れていても突き合わせを諦めるだけで再生は止めない
        // The manifest is external input from another machine; a broken one only forfeits the check, never stops the replay
        private static JObject ReadRecordedServerData(string bundleDirectory)
        {
            var manifestPath = Path.Combine(bundleDirectory, ManifestFileName);
            if (!File.Exists(manifestPath)) return null;

            // 外部JSONのパースは境界。ここで閉じないと壊れた箱1つで再現ツールが落ちる
            // Parsing external JSON is a boundary; without containing it here one broken box takes the tool down
            try
            {
                return JObject.Parse(File.ReadAllText(manifestPath))["serverData"] as JObject;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"バグ報告バンドルの {ManifestFileName} を読めませんでした: {exception.GetBaseException().Message} path:{manifestPath}");
                return null;
            }
        }
    }
}
