using System;
using System.IO;
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
        // Unityのビルドコールバックは PlayerBuildRequest を受け取れないため、BuildPipeline が BuildPlayer 直前に押し込む
        // BuildPipeline を通らないビルド（Build Settings 画面等）は既定の非strict（CI互換）で焼く
        // Unity's build callback cannot receive the PlayerBuildRequest, so BuildPipeline pushes this right before BuildPlayer
        // Builds that bypass BuildPipeline (e.g. the Build Settings window) bake with the default non-strict, CI-compatible mode
        private static bool _isStrictBundling;

        public int callbackOrder => 1;

        public static void SetStrictBundling(bool isStrictBundling)
        {
            _isStrictBundling = isStrictBundling;
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            var repo = RepositoryStateProbe.ProbeGit(RepositoryStateProbe.RepositoryRoot);
            // 正本cloneの隣ではなく同梱元と同じ repo の HEAD を焼く。worktree ビルドで両者は別ディレクトリになりうる
            // Bake the HEAD of the very repo that gets bundled, not the primary clone's neighbour; they can differ in a worktree build
            var masterData = RepositoryStateProbe.ProbeGit(GameDataBundler.MasterDataRepositoryRoot);
            var pinned = MasterDataRootLocator.ReadPinnedCommit(RepositoryStateProbe.RepositoryRoot, out var pinUnreadableReason);
            var label = Environment.GetEnvironmentVariable(BuildInfoComposer.SteamBuildLabelEnvKey) ?? "";
            var json = BuildInfoComposer.Compose(repo, masterData, pinned, pinUnreadableReason, label, DateTime.UtcNow, report.summary.platform.ToString(), _isStrictBundling, out var buildFailureReason);

            // strict の配布物で出所を偽る焼き込みは作らせない。理由はビルド失敗メッセージに出す
            // A strict distribution build must not bake a misreported origin; the reason goes into the build failure message
            if (buildFailureReason != null) throw new BuildFailedException("[BuildInfoWriter] " + buildFailureReason);

            var path = GameSystemPaths.BuildInfoFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json);
            Debug.Log($"[BuildInfoWriter] build-info.json を書きました commit:{repo.State?.Commit} dirty:{repo.State?.Dirty} masterData:{masterData.State?.Commit} label:{label} strict:{_isStrictBundling}");
        }
    }
}
