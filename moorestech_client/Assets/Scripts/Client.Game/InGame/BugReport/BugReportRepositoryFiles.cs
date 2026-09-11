using System.IO;

namespace Client.Game.InGame.BugReport
{
    // 未コミットのファイル変更まで再現対象に含めるため、差分と未追跡ファイルをバンドルへ写す（読み取りのみ）
    // Copies the diff and untracked files into the bundle so uncommitted edits stay reproducible; read-only throughout
    public static class BugReportRepositoryFiles
    {
        // 未追跡ファイルには作業ツリー限りの資格情報が混ざりうる。バンドルへ入れずに除外した事実だけを残す
        // Untracked files can hide working-tree-only credentials; they stay out of the bundle and only the exclusion is recorded
        private static readonly string[] SecretFileNameMarkers = { ".env", ".pem", ".key", ".p12", ".keystore", "id_rsa", "credential", "secret", "token" };

        public static void Write(string directory, BugReportManifest manifest, RepositoryState buildInfo)
        {
            var repo = Path.Combine(directory, "repo");
            Directory.CreateDirectory(repo);

            // ビルド実行にはgitも作業ツリーも無い。焼き込んだコミットだけを載せ、取れない分は理由付きで欠損に残す
            // A build has neither git nor a working tree; it carries only the baked commit and records the rest as missing
            if (buildInfo != null)
            {
                manifest.Repository = buildInfo;
                manifest.MasterData = new RepositoryState { Commit = "", Branch = "", Dirty = false };
                if (string.IsNullOrEmpty(buildInfo.Commit)) manifest.AddMissing("repository", "build-info.jsonが無くリポジトリ状態が不明");
                manifest.AddMissing("repo/head.diff", "ビルド実行のため未コミット差分は取れない");
                manifest.AddMissing("masterData", "ビルド実行のためマスタデータのリポジトリ状態は取れない");
                return;
            }

            manifest.Repository = WriteOne(RepositoryStateProbe.RepositoryRoot, repo, "head.diff", "untracked", manifest);
            manifest.MasterData = WriteOne(RepositoryStateProbe.MasterDataRoot, repo, "master.diff", "master-untracked", manifest);
        }

        private static RepositoryState WriteOne(string root, string repoDirectory, string diffName, string untrackedDirectoryName, BugReportManifest manifest)
        {
            var probe = RepositoryStateProbe.ProbeGit(root);
            if (probe.Error != null)
            {
                manifest.AddMissing(diffName, probe.Error);
                return new RepositoryState { Commit = "", Branch = "", Dirty = false };
            }

            File.WriteAllText(Path.Combine(repoDirectory, diffName), probe.DiffText);
            File.WriteAllText(Path.Combine(repoDirectory, untrackedDirectoryName + ".txt"), string.Join("\n", probe.UntrackedFiles));
            long copied = 0;
            foreach (var relative in probe.UntrackedFiles)
            {
                var source = Path.Combine(root, relative);
                if (!File.Exists(source)) continue;
                if (IsLikelySecret(relative))
                {
                    manifest.AddMissing(relative, "秘密情報を含みうる名前のためバンドルへ入れなかった");
                    continue;
                }
                copied += new FileInfo(source).Length;
                if (copied > BugReportBundleWriter.UntrackedBytesLimit)
                {
                    manifest.AddMissing(untrackedDirectoryName, "未追跡ファイルが20MBを超えたため一覧のみ");
                    break;
                }
                var target = Path.Combine(repoDirectory, untrackedDirectoryName, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(source, target);
            }
            return probe.State;
        }

        private static bool IsLikelySecret(string relativePath)
        {
            var name = Path.GetFileName(relativePath).ToLowerInvariant();
            foreach (var marker in SecretFileNameMarkers)
            {
                if (name.Contains(marker)) return true;
            }
            return false;
        }
    }
}
