using System;
using UnityEngine;

namespace Client.Game.InGame.BugReport
{
    // 記録時にサーバーがマスタとmodを読んだ場所。受け側は別マシンの別パスへ展開するので相対でも表す
    // Where the server read masters and mods at record time; the receiving side unpacks elsewhere, so it is also expressed relatively
    public sealed class ServerDataLocation
    {
        public const string RepositoryRootName = "repository";
        public const string MasterDataRootName = "masterData";
        public const string AbsoluteRootName = "absolute";

        public string Path;

        // 相対の基準（repository / masterData / absolute）。受け側はこれを見て自分の worktree 側で解決する
        // Which root the relative path is against; the receiving side resolves it inside its own worktree
        public string RelativeTo;
        public string RelativePath;

        // 再現は記録時と同じサーバーデータでしか成立しない。サーバーが申告した置き場だけを載せ、推測では埋めない
        // Reproduction only holds with the very server data used at record time; only the server's own report is recorded, never a guess
        public static void Record(string serverDataDirectory, BugReportManifest manifest, string repositoryRoot, string masterDataRoot)
        {
            if (string.IsNullOrEmpty(serverDataDirectory))
            {
                manifest.AddMissing("serverData", "サーバーがマスタを読んだ置き場を申告しなかった（再現側はマスタを特定できない）");
                return;
            }

            manifest.ServerData = Resolve(serverDataDirectory, repositoryRoot, masterDataRoot);
            if (manifest.ServerData.RelativePath.Length == 0) manifest.AddMissing("serverData/relativePath", $"サーバーデータがリポジトリの外にある path:{manifest.ServerData.Path}");
        }

        // どちらのリポジトリの配下でもないサーバーデータは受け側で解決できない。絶対パスだけを残して理由を告げる
        // Server data under neither repository cannot be resolved by the receiver; only the absolute path is kept and the reason is told
        public static ServerDataLocation Resolve(string serverDataDirectory, string repositoryRoot, string masterDataRoot)
        {
            var full = System.IO.Path.GetFullPath(serverDataDirectory);
            if (TryRelative(repositoryRoot, full, out var repositoryRelative)) return new ServerDataLocation { Path = full, RelativeTo = RepositoryRootName, RelativePath = repositoryRelative };
            if (TryRelative(masterDataRoot, full, out var masterRelative)) return new ServerDataLocation { Path = full, RelativeTo = MasterDataRootName, RelativePath = masterRelative };

            Debug.LogWarning($"バグ報告: サーバーデータがリポジトリの外にあるため受け側で解決できません path:{full} repo:{repositoryRoot} master:{masterDataRoot}");
            return new ServerDataLocation { Path = full, RelativeTo = AbsoluteRootName, RelativePath = "" };
        }

        // 区切りは受け側（bash/python）が読むので常に '/' に揃える
        // The separator is normalized to '/' because the receiving side (bash/python) reads it
        private static bool TryRelative(string root, string fullPath, out string relative)
        {
            relative = "";
            var normalizedRoot = System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)) return false;
            relative = fullPath.Substring(normalizedRoot.Length).Replace(System.IO.Path.DirectorySeparatorChar, '/');
            return relative.Length > 0;
        }
    }
}
