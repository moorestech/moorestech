using System;
using System.IO;
using Client.Game.InGame.BugReport;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Client.Editor.Build
{
    // ビルドにリポジトリ状態を焼き込む。バグ報告の manifest がビルド版でも出所を書けるようにする
    // Bakes the repository state into the build so a bug report's manifest can name its origin in a player build
    public class BuildInfoWriter : IPreprocessBuildWithReport
    {
        public int callbackOrder => 1;

        public void OnPreprocessBuild(BuildReport report)
        {
            var repo = RepositoryStateProbe.ProbeGit(RepositoryStateProbe.RepositoryRoot);
            var master = RepositoryStateProbe.ProbeGit(RepositoryStateProbe.MasterDataRoot);
            var path = Path.Combine(Application.streamingAssetsPath, RepositoryStateProbe.BuildInfoFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, RepositoryStateProbe.ComposeBuildInfoJson(repo, master, DateTime.UtcNow));
            Debug.Log($"build-info.json を書きました commit:{repo.State?.Commit} dirty:{repo.State?.Dirty}");
        }
    }
}
