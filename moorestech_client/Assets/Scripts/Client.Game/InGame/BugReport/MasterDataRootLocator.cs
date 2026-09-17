using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.Game.InGame.BugReport
{
    // マスタデータのリポジトリがどこにあるかを、コミット済みのピン（.moorestech-external-revisions.json）の relativePath から解決する
    // Resolves where the master-data repository sits from the relativePath in the committed pin (.moorestech-external-revisions.json)
    public static class MasterDataRootLocator
    {
        private const string ExternalRevisionsFileName = ".moorestech-external-revisions.json";
        private const string MasterRepositoryKey = "moorestech_master";
        private const string UnresolvedMasterDataDirectoryName = ".moorestech-master-unresolved";

        public static string Resolve()
        {
            if (!TryReadRelativePath(RepositoryStateProbe.RepositoryRoot, out var relativePath, out var reason)) return UnresolvedMasterDataRoot(reason);

            // relativePath は正本repoからの相対。worktreeから起動されても正本の隣を見るため共通gitディレクトリで正本を特定する
            // relativePath is relative to the primary repo, so the common git directory locates it even when running from a worktree
            if (!RepositoryStateProbe.TryGit(RepositoryStateProbe.RepositoryRoot, "rev-parse --path-format=absolute --git-common-dir", out var commonGitDirectory, out var error)) return UnresolvedMasterDataRoot(error);

            var primaryRepositoryRoot = Directory.GetParent(commonGitDirectory.Trim());
            if (primaryRepositoryRoot == null) return UnresolvedMasterDataRoot($"共通gitディレクトリの親を取れない dir:{commonGitDirectory.Trim()}");
            return Path.GetFullPath(Path.Combine(primaryRepositoryRoot.FullName, relativePath));
        }

        // ビルドが同梱するマスタrepo。worktreeでも正本でなくビルドする checkout 自身から relativePath を解く（同梱元と焼くコミットを同じ場所に揃える・D-6）
        // The master repo a build bundles: relativePath is resolved from the building checkout itself, not the primary clone, so the bundled source and baked commit share one place (D-6)
        public static string ResolveForBuildingCheckout(string checkoutRoot)
        {
            if (!TryReadRelativePath(checkoutRoot, out var relativePath, out var reason)) return UnresolvedMasterDataRoot(reason);
            return Path.GetFullPath(Path.Combine(checkoutRoot, relativePath));
        }

        // ビルドに焼く masterDataCommit の突き合わせ先。読めないときは null と理由を返し、縮退か失敗かとそのログは呼び出し側が決める
        // The pin the baked masterDataCommit is checked against; when unreadable it returns null plus a reason, leaving degrade-or-fail and its logging to the caller
        // 作業ツリーのピンはGUIビルドの同期が実HEADへ書き戻すため、照合元はコミット済みの値に固定する（同値比較で素通りさせない）
        // The working-tree pin can be rewritten to the actual HEAD by the GUI build sync, so the committed value is the source, never a self-comparison
        public static string ReadPinnedCommit(string repositoryRoot, out string unreadableReason)
        {
            unreadableReason = null;
            var committedPinSource = $"HEAD:{ExternalRevisionsFileName} repo:{repositoryRoot}";
            if (!Directory.Exists(repositoryRoot))
            {
                unreadableReason = $"コミット済みのピンを読めない（ディレクトリが無い） {committedPinSource}";
                return null;
            }
            if (!RepositoryStateProbe.TryGit(repositoryRoot, $"show HEAD:{ExternalRevisionsFileName}", out var committedPinJson, out var error))
            {
                unreadableReason = $"コミット済みのピンを読めない {committedPinSource}: {error}";
                return null;
            }

            var commit = ReadMasterPinField(committedPinJson, committedPinSource, "commitHash");
            if (!string.IsNullOrEmpty(commit)) return commit;
            unreadableReason = $"ピンに {MasterRepositoryKey} の commitHash が無い {committedPinSource}";
            return null;
        }

        private static bool TryReadRelativePath(string repositoryRoot, out string relativePath, out string reason)
        {
            reason = null;
            var revisionsPath = Path.Combine(repositoryRoot, ExternalRevisionsFileName);
            relativePath = File.Exists(revisionsPath) ? ReadMasterPinField(File.ReadAllText(revisionsPath), revisionsPath, "relativePath") : null;
            if (!File.Exists(revisionsPath)) reason = $"ピンファイルが無い path:{revisionsPath}";
            else if (string.IsNullOrEmpty(relativePath)) reason = $"ピンに {MasterRepositoryKey} の relativePath が無い path:{revisionsPath}";
            return reason == null;
        }

        // 解決できないまま推測パスを名乗ると別repoの差分が混ざる。実在しない場所を返し、突き合わせを全て外したうえで理由を残す
        // Naming a guessed path would mix another repo's diff in, so an unreachable path fails every match and the reason is logged
        private static string UnresolvedMasterDataRoot(string reason)
        {
            Debug.LogWarning($"マスタデータのリポジトリの置き場を解決できません: {reason}");
            return Path.Combine(RepositoryStateProbe.RepositoryRoot, UnresolvedMasterDataDirectoryName);
        }

        // ピンは外部入力のJSON。壊れた1ファイルでバグ報告ごと落とさないよう、ここだけ解析失敗を理由へ変換する
        // The pin is external JSON input; only here a parse failure becomes a reason so one broken file never kills the whole report
        private static string ReadMasterPinField(string pinJson, string pinSource, string fieldName)
        {
            try
            {
                foreach (var revision in (JArray)JObject.Parse(pinJson)["repositories"])
                {
                    if ((string)revision["key"] == MasterRepositoryKey) return (string)revision[fieldName];
                }
                return null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"ピンファイルを読めません source:{pinSource}: {exception.GetBaseException().Message}");
                return null;
            }
        }
    }
}
