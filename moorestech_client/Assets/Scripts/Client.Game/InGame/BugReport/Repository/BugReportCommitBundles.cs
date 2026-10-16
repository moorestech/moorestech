using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Game.Paths;

namespace Client.Game.InGame.BugReport
{
    public static class BugReportCommitBundles
    {
        // 旧箱は元のmanifestのSHAだけを補完し、記録時のメタデータを変えない
        // Complete legacy evidence using only original manifest SHAs without changing capture metadata
        public static string CompleteExisting(string boxDirectory, string repositoryRoot, string masterDataRoot, string declarationMarkerPath)
        {
            var repoDirectory = Path.Combine(boxDirectory, BugReportBundleLayout.RepositoryDirectoryName);
            var statusPath = Path.Combine(repoDirectory, BugReportBundleLayout.BundleStatusFileName);
            var statuses = File.Exists(statusPath) ? File.ReadAllLines(statusPath) : Array.Empty<string>();
            if (new[] { BugReportBundleLayout.CommitsBundleFileName, BugReportBundleLayout.MasterCommitsBundleFileName }
                .All(name => statuses.Any(line => line.StartsWith(name + ": ", StringComparison.Ordinal) && !line.Contains(": failed-delivery"))))
                return null; // silent-ok: 両資料の補完結果あり / 配送前に実体を再検証する
            var path = Path.Combine(boxDirectory, BugReportBundleLayout.ManifestFileName);
            var original = JObject.Parse(File.ReadAllText(path));
            if (original["isEditor"]?.Type != JTokenType.Boolean) return "manifest.isEditor が不明のため元repoを特定できません";
            if (!(bool)original["isEditor"]) return null; // silent-ok: ビルド箱にEditorのgit資料を足さない / buildInfoで出所を表示
            if (File.Exists(declarationMarkerPath)) return "既宣言の旧箱には資料を追加できません。元箱を維持して復旧を依頼してください";
            var repository = original["repository"]?.ToObject<RepositoryState>();
            var master = original["masterData"]?.ToObject<RepositoryState>();
            var failure = Validate(repositoryRoot, repository?.Commit) ?? Validate(masterDataRoot, master?.Commit);
            if (failure != null) return failure;
            if (original["missing"] is not JArray missing) return "manifest.missing が配列でないため資料欠損を記録できません";
            var manifest = new BugReportManifest { Repository = repository, MasterData = master };
            Write(repoDirectory, Path.GetFileName(boxDirectory), manifest, repositoryRoot, masterDataRoot);
            foreach (var item in manifest.Missing) missing.Add(new JObject { ["item"] = item.Item, ["reason"] = item.Reason });
            File.WriteAllText(path, original.ToString());
            return manifest.Missing.Count == 0 ? null : "元コミットの資料補完に失敗しました。manifest.missing を確認してください";

            #region Internal

            string Validate(string root, string commit)
            {
                if (!Directory.Exists(root) || commit == null || !Regex.IsMatch(commit, "^[0-9a-fA-F]{40}$"))
                    return "元repoまたはmanifestのSHAを特定できないため送信を保留しました";
                return RepositoryStateProbe.TryGit(root, $"cat-file -e {commit}^{{commit}}", out _, out var reason)
                    ? null : $"元SHA {commit} を指定repoで確認できません: {reason}";
            }

            #endregion
        }

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
                if (string.IsNullOrEmpty(commit)) { Record(label, CommitBundleResultKind.SkippedNoCommit, null, null); return; }
                if (!Directory.Exists(root)) { Record(label, CommitBundleResultKind.SkippedNoRepository, root, null); return; }
                if (!RepositoryStateProbe.TryGit(root, $"cat-file -e {commit}^{{commit}}", out _, out var error))
                {
                    Record(label, CommitBundleResultKind.CommitNotFound, commit, error);
                    return;
                }

                // 受信側が取得できるoriginの公開済みSHAだけ省略する
                // Omit only published SHAs reachable from the receiver's origin
                if (!RepositoryStateProbe.TryGit(root, $"for-each-ref --contains {commit} --format=%(refname) refs/remotes/origin/", out var remoteRefs, out error))
                {
                    Record(label, CommitBundleResultKind.RemoteRefsQueryFailed, null, error);
                    return;
                }
                if (!string.IsNullOrWhiteSpace(remoteRefs))
                {
                    Record(label, CommitBundleResultKind.AlreadyPublished, null, null);
                    return;
                }

                // bundleは名前付きrefを要求する。作業ツリーには触れず、一時refを必ず解放する
                // Bundles require a named ref; never touch the working tree and always release the temporary ref
                var reference = "refs/bugreport/" + bundleId + "-" + Guid.NewGuid().ToString("N");
                var output = Path.GetFullPath(Path.Combine(repoDirectory, label));
                var temporary = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
                // 外部gitプロセスとディスクIOの境界で、この資料の失敗を欠損へ残す
                // At the external git process and disk IO boundary, record this evidence failure as missing
                try
                {
                    Directory.CreateDirectory(repoDirectory);
                    if (!RepositoryStateProbe.TryGit(root, $"update-ref {reference} {commit}", out _, out error))
                    {
                        Record(label, CommitBundleResultKind.UpdateRefFailed, null, error);
                        return;
                    }
                    if (RepositoryStateProbe.TryGit(root, $"bundle create \"{temporary}\" {reference}", out _, out error))
                    {
                        if (File.Exists(output)) File.Replace(temporary, output, null);
                        else File.Move(temporary, output);
                        Record(label, CommitBundleResultKind.Created, null, null);
                    }
                    else
                    {
                        Record(label, CommitBundleResultKind.BundleCreateFailed, null, error);
                    }
                }
                // 外部ディスクIOの失敗をこの資料だけへ隔離する
                // Isolate external disk IO failures to this evidence item
                catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
                {
                    Record(label, CommitBundleResultKind.BundleCreateFailed, null, e.Message);
                }
                finally
                {
                    // 自分の一時ファイルだけを片付け、他writerの公開済み資料は消さない
                    // Clean only this invocation's temporary output, never another writer's published evidence
                    try { File.Delete(temporary); }
                    catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
                    { manifest.AddMissing("repo/" + label, "一時出力を削除できなかった: " + e.Message); }
                    if (!RepositoryStateProbe.TryGit(root, $"update-ref -d {reference}", out _, out var cleanupError))
                        manifest.AddMissing("repo/" + label, "一時refを削除できなかった: " + cleanupError);
                }

            }

            void Record(string label, CommitBundleResultKind kind, string context, string reason)
            {
                var state = kind switch
                {
                    CommitBundleResultKind.SkippedNoCommit => "skipped-no-commit",
                    CommitBundleResultKind.SkippedNoRepository => $"skipped-no-repo ({context})",
                    CommitBundleResultKind.AlreadyPublished => "not-needed-remote-has-commit",
                    CommitBundleResultKind.CommitNotFound => $"failed-commit-not-found ({context})",
                    CommitBundleResultKind.RemoteRefsQueryFailed => "failed-remote-refs-query",
                    CommitBundleResultKind.UpdateRefFailed => "failed-update-ref",
                    CommitBundleResultKind.Created => "created",
                    CommitBundleResultKind.BundleCreateFailed => "failed-bundle-create",
                    _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown bundle result"),
                };
                status.Append(label).Append(": ").Append(state).Append('\n');
                if (kind is CommitBundleResultKind.CommitNotFound or CommitBundleResultKind.RemoteRefsQueryFailed or CommitBundleResultKind.UpdateRefFailed or CommitBundleResultKind.BundleCreateFailed)
                    manifest.AddMissing("repo/" + label, reason ?? state);
            }

            #endregion
        }
    }
}
