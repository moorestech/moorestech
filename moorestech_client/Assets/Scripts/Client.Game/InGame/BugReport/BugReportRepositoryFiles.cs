using System;
using System.IO;
using Client.Game.InGame.BugReport.BuildOrigin;
using Game.Paths;

namespace Client.Game.InGame.BugReport
{
    // 未コミットのファイル変更まで再現対象に含めるため、差分と未追跡ファイルをバンドルへ写す（読み取りのみ）
    // Copies the diff and untracked files into the bundle so uncommitted edits stay reproducible; read-only throughout
    public static class BugReportRepositoryFiles
    {
        private const string HeadDiffFileName = "head.diff";
        private const string MasterDiffFileName = "master.diff";

        // 未追跡ファイルには作業ツリー限りの資格情報が混ざりうる。バンドルへ入れずに除外した事実だけを残す
        // Untracked files can hide working-tree-only credentials; they stay out of the bundle and only the exclusion is recorded
        private static readonly string[] SecretFileNameMarkers = { ".env", ".pem", ".key", ".p12", ".keystore", "id_rsa", "credential", "secret", "token" };

        // リポジトリの場所は Application.dataPath 由来でメインスレッドでしか読めない。呼び出し側が読んだ値を受け取る
        // The roots derive from Application.dataPath, readable only on the main thread, so the caller passes what it read
        // 分岐は出所の3状態だけを見る。Editor以外で git probe に入ると、配布先に偶然ある無関係な作業ツリーを名乗ってしまう（F01）
        // The branch looks only at the three origin states; entering the git probe outside the Editor would claim some unrelated working tree on the player's machine (F01)
        public static void Write(string directory, BugReportManifest manifest, BuildOriginReading buildOrigin, string repositoryRoot, string masterDataRoot)
        {
            switch (buildOrigin.Kind)
            {
                case BuildOriginKind.Editor:
                    WriteWorkingTreeState(directory, manifest, repositoryRoot, masterDataRoot);
                    return;
                case BuildOriginKind.BakedBuild:
                    WriteBakedState(manifest, buildOrigin);
                    return;
                case BuildOriginKind.BuildWithoutInfo:
                    WriteUnknownOrigin(manifest, buildOrigin.MissingReason);
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(buildOrigin), buildOrigin.Kind, "未知の出所の種類");
            }
        }

        // ディスクは外部資源。本体repoとマスタrepoを別々に隔離し、片方の失敗でもう片方の状態まで落とさない
        // Disk is an external resource; the two repositories are isolated separately so one failure never drops the other's state
        private static void WriteWorkingTreeState(string directory, BugReportManifest manifest, string repositoryRoot, string masterDataRoot)
        {
            var repo = Path.Combine(directory, BugReportBundleLayout.RepositoryDirectoryName);
            manifest.Repository = WriteIsolated(repositoryRoot, repo, HeadDiffFileName, "untracked", manifest);
            manifest.MasterData = WriteIsolated(masterDataRoot, repo, MasterDiffFileName, "master-untracked", manifest);
        }

        // ビルド実行にはgitも作業ツリーも無い。焼き込んだ状態だけを載せ、取れない分は理由付きで欠損に残す
        // A build has neither git nor a working tree; it carries only the baked state and records the rest as missing
        private static void WriteBakedState(BugReportManifest manifest, BuildOriginReading buildOrigin)
        {
            var baked = BuildInfoJson.ToBugReportBuildInfo(buildOrigin.BuildInfo);
            manifest.Repository = baked.Repository;
            foreach (var missing in buildOrigin.Missing) manifest.AddMissing(missing.Item, missing.Reason);
            manifest.AddMissing($"{BugReportBundleLayout.RepositoryDirectoryName}/{HeadDiffFileName}", "ビルド実行のため未コミット差分は取れない");

            // 焼き込まれたマスタの状態は読めたときだけ載せる。空の状態を載せると別マスタでの再現がクリーン扱いになる
            // The baked master state is claimed only when it was readable; an empty one would make a different master look clean
            if (baked.MasterData != null) manifest.MasterData = baked.MasterData;
            else manifest.AddMissing("masterData", "ビルドにマスタデータのリポジトリ状態が焼かれていない");
            manifest.AddMissing($"{BugReportBundleLayout.RepositoryDirectoryName}/{MasterDiffFileName}", "ビルド実行のためマスタデータの未コミット差分は取れない");
        }

        // 焼き込み情報の無い配布ビルドは、どのコミットかを名乗れない。空の状態も作業ツリーの状態も載せずnullのまま理由を残す
        // A distributed build without baked info cannot name its commit; neither an empty state nor a working tree's is claimed, only the reason
        private static void WriteUnknownOrigin(BugReportManifest manifest, string reason)
        {
            manifest.Repository = null;
            manifest.MasterData = null;
            manifest.AddMissing("repository", reason);
            manifest.AddMissing("masterData", reason);
        }

        // ディスクIOは外部境界。握るのはディスク由来の失敗だけで、実装バグは握らず呼び出し側へ抜けさせる
        // Disk IO is an external boundary; only disk failures are swallowed while implementation defects still escape to the caller
        private static RepositoryState WriteIsolated(string root, string repoDirectory, string diffName, string untrackedDirectoryName, BugReportManifest manifest)
        {
            try
            {
                return WriteOne(root, repoDirectory, diffName, untrackedDirectoryName, manifest);
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                manifest.AddMissing(diffName, $"リポジトリ状態の書き出しに失敗した: {e.Message}");
                return null;
            }
        }

        private static RepositoryState WriteOne(string root, string repoDirectory, string diffName, string untrackedDirectoryName, BugReportManifest manifest)
        {
            Directory.CreateDirectory(repoDirectory);
            var probe = RepositoryStateProbe.ProbeGit(root);
            if (probe.Error != null)
            {
                manifest.AddMissing(diffName, probe.Error);
                return null;
            }

            // 問い合わせの失敗は1件ずつ箱へ残す。受け側はこれが無いと「差分なし」と「取れなかった」を区別できない
            // Each failed query lands in the box one by one; without them the receiving side cannot tell "no diff" from "unreadable"
            foreach (var failure in probe.QueryFailures) manifest.AddMissing(diffName, failure);

            // 追跡ファイルに変更があるのに差分が空なら、差分の取得が壊れている。黙って空の差分を運ぶと再現が別のコードになる
            // An empty diff while tracked files have changes means the diff itself failed; shipping it silently reproduces different code
            if (probe.TrackedChangesPresent && probe.DiffText.Trim().Length == 0) manifest.AddMissing(diffName, "追跡ファイルに変更があるのに差分が空だった");

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
