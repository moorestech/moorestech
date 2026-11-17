using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Client.Editor.Build
{
    /// <summary>
    /// BuildPipeline を通らないビルド（Build Settings 画面・地形 QA の BuildPlayer 直呼び）でも撤去前の Web UI の成果物を焼く前に消す入口
    /// Entry that removes leftover pre-removal Web UI output before baking even for builds bypassing BuildPipeline (Build Settings window, direct BuildPlayer in terrain QA)
    /// </summary>
    public class PreRemovalStreamingAssetsCleanupBuildHook : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            // 消せなければ撤去前の Web の dist が成果物へ入るので、ビルドを止める（理由は Cleaner が LogError へ出す）
            // If removal fails the pre-removal Web dist would ship, so stop the build (the Cleaner logs the reason as an error)
            if (!PreRemovalStreamingAssetsCleaner.RemoveLeftovers()) throw new BuildFailedException("[PreRemovalStreamingAssetsCleanupBuildHook] 撤去前の Web UI の成果物を消せないためビルドを止めます");
        }
    }
}
