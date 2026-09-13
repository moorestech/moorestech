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
            var revisionsPath = Path.Combine(RepositoryStateProbe.RepositoryRoot, ExternalRevisionsFileName);
            if (!File.Exists(revisionsPath)) return UnresolvedMasterDataRoot($"ピンファイルが無い path:{revisionsPath}");

            var relativePath = ReadMasterRelativePath(revisionsPath);
            if (string.IsNullOrEmpty(relativePath)) return UnresolvedMasterDataRoot($"ピンに {MasterRepositoryKey} の relativePath が無い path:{revisionsPath}");

            // relativePath は正本repoからの相対。worktreeから起動されても正本の隣を見るため共通gitディレクトリで正本を特定する
            // relativePath is relative to the primary repo, so the common git directory locates it even when running from a worktree
            if (!RepositoryStateProbe.TryGit(RepositoryStateProbe.RepositoryRoot, "rev-parse --path-format=absolute --git-common-dir", out var commonGitDirectory, out var error)) return UnresolvedMasterDataRoot(error);

            var primaryRepositoryRoot = Directory.GetParent(commonGitDirectory.Trim());
            if (primaryRepositoryRoot == null) return UnresolvedMasterDataRoot($"共通gitディレクトリの親を取れない dir:{commonGitDirectory.Trim()}");
            return Path.GetFullPath(Path.Combine(primaryRepositoryRoot.FullName, relativePath));
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
        private static string ReadMasterRelativePath(string revisionsPath)
        {
            try
            {
                foreach (var revision in (JArray)JObject.Parse(File.ReadAllText(revisionsPath))["repositories"])
                {
                    if ((string)revision["key"] == MasterRepositoryKey) return (string)revision["relativePath"];
                }
                return null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"ピンファイルを読めません path:{revisionsPath}: {exception.GetBaseException().Message}");
                return null;
            }
        }
    }
}
