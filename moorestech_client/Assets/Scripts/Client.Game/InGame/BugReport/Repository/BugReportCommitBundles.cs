using System;
using System.IO;
using System.Text;
using Game.Paths;

namespace Client.Game.InGame.BugReport
{
    public static class BugReportCommitBundles
    {
        public static void Write(string repoDirectory, string bundleId, BugReportManifest manifest, string repositoryRoot, string masterDataRoot)
        {
            var status = new StringBuilder();
            WriteOne(BugReportBundleLayout.CommitsBundleFileName, repositoryRoot, manifest.Repository?.Commit);
            WriteOne(BugReportBundleLayout.MasterCommitsBundleFileName, masterDataRoot, manifest.MasterData?.Commit);

            // 状態ファイルも外部ディスク境界。失敗は箱の欠損とログへ残す
            // The status file is an external disk boundary too; failures reach both the bundle and the log
            try
            {
                Directory.CreateDirectory(repoDirectory);
                File.WriteAllText(Path.Combine(repoDirectory, BugReportBundleLayout.BundleStatusFileName), status.ToString());
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                manifest.AddMissing("repo/" + BugReportBundleLayout.BundleStatusFileName, e.Message);
            }

            #region Internal

            void WriteOne(string label, string root, string commit)
            {
                if (string.IsNullOrEmpty(commit)) { Record("skipped-no-commit", null); return; }
                if (!Directory.Exists(root)) { Record($"skipped-no-repo ({root})", null); return; }
                if (RepositoryStateProbe.TryGit(root, $"merge-base --is-ancestor {commit} origin/master", out _, out _))
                {
                    Record("not-needed-origin-has-commit", null);
                    return;
                }
                if (!RepositoryStateProbe.TryGit(root, $"cat-file -e {commit}^{{commit}}", out _, out var error))
                {
                    Record($"failed-commit-not-found ({commit})", error);
                    return;
                }

                // bundleは名前付きrefを要求する。作業ツリーには触れず、一時refを必ず解放する
                // Bundles require a named ref; never touch the working tree and always release the temporary ref
                var reference = "refs/bugreport/" + bundleId;
                var output = Path.GetFullPath(Path.Combine(repoDirectory, label));
                try
                {
                    Directory.CreateDirectory(repoDirectory);
                    if (!RepositoryStateProbe.TryGit(root, $"update-ref {reference} {commit}", out _, out error))
                    {
                        Record("failed-update-ref", error);
                        return;
                    }
                    if (RepositoryStateProbe.TryGit(root, $"bundle create \"{output}\" origin/master..{reference}", out _, out error))
                        Record("created", null);
                    else
                    {
                        File.Delete(output);
                        Record("failed-bundle-create", error);
                    }
                }
                // 外部ディスクIOの失敗をこの資料だけへ隔離する
                // Isolate external disk IO failures to this evidence item
                catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
                {
                    Record("failed-bundle-create", e.Message);
                }
                finally
                {
                    if (!RepositoryStateProbe.TryGit(root, $"update-ref -d {reference}", out _, out var cleanupError))
                        manifest.AddMissing("repo/" + label, "一時refを削除できなかった: " + cleanupError);
                }

                void Record(string state, string reason)
                {
                    status.Append(label).Append(": ").Append(state).Append('\n');
                    if (state.StartsWith("failed-", StringComparison.Ordinal))
                        manifest.AddMissing("repo/" + label, reason ?? state);
                }
            }

            #endregion
        }
    }
}
