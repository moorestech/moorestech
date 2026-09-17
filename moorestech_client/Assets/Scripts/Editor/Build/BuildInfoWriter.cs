using System;
using System.IO;
using Client.Editor.Build.Bundlers;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.BuildOrigin;
using Game.Paths;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Client.Editor.Build
{
    // ビルドに出所を焼き込む（shared-contracts §1）
    // - コミット
    // - master dataピン
    // - Steamビルド識別
    // Bakes the origin into the build (shared-contracts §1)
    // - commit
    // - master-data pin
    // - Steam build label
    public class BuildInfoWriter : IPreprocessBuildWithReport
    {
        public int callbackOrder => 1;

        // BuildPipeline を通らないビルド（Build Settings 画面等）を非strictで焼くための入口
        // BuildPipeline 経由では BuildPlayer 直前の Write(strict) が関門を済ませており、ここは同じ状態を非strictで焼き直すだけ
        // Entry that bakes builds bypassing BuildPipeline (e.g. the Build Settings window) in non-strict mode
        // Via BuildPipeline the Write(strict) right before BuildPlayer already gated the build, so this only re-bakes the same state non-strict
        public void OnPreprocessBuild(BuildReport report)
        {
            Write(false, report.summary.platform);
        }

        internal static void Write(bool isStrictBundling, UnityEditor.BuildTarget target)
        {
            var repo = RepositoryStateProbe.ProbeGit(RepositoryStateProbe.RepositoryRoot);
            // 正本cloneの隣ではなく同梱元と同じ repo の HEAD を焼く。worktree ビルドで両者は別ディレクトリになりうる
            // Bake the HEAD of the very repo that gets bundled, not the primary clone's neighbour; they can differ in a worktree build
            var masterData = RepositoryStateProbe.ProbeGit(GameDataBundler.MasterDataRepositoryRoot);
            var pinned = MasterDataRootLocator.ReadPinnedCommit(RepositoryStateProbe.RepositoryRoot, out var pinUnreadableReason);
            var label = Environment.GetEnvironmentVariable(BuildInfoComposer.SteamBuildLabelEnvKey);
            var branch = Environment.GetEnvironmentVariable(BuildInfoComposer.BuildBranchEnvKey);
            var json = BuildInfoComposer.Compose(repo, masterData, pinned, pinUnreadableReason, label, branch, DateTime.UtcNow, target.ToString(), isStrictBundling, out var buildFailureReason);

            // strict の配布物で出所を偽る焼き込みは作らせない。理由はビルド失敗メッセージに出す
            // A strict distribution build must not bake a misreported origin; the reason goes into the build failure message
            if (buildFailureReason != null) throw new BuildFailedException("[BuildInfoWriter] " + buildFailureReason);

            // 未コミット変更入りのmasterはコミットでは中身を特定できないため、配布では拒否する
            // A dirty master cannot be identified by its commit, so distribution builds refuse it
            if (masterData.State?.Dirty == true)
            {
                var dirtyReason = $"同梱元の master data に未コミット変更がある root:{GameDataBundler.MasterDataRepositoryRoot} commit:{masterData.State.Commit}";
                if (isStrictBundling) throw new BuildFailedException("[BuildInfoWriter] " + dirtyReason);
                Debug.LogWarning($"[BuildInfoWriter] CI互換（非strict）のため焼き込みを続けます: {dirtyReason}");
            }

            var path = GameSystemPaths.BuildInfoFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json);
            Debug.Log($"[BuildInfoWriter] build-info.json を書きました commit:{repo.State?.Commit} dirty:{repo.State?.Dirty} masterData:{masterData.State?.Commit} label:{label} branchOverride:{branch} strict:{isStrictBundling}");
        }
    }
}
