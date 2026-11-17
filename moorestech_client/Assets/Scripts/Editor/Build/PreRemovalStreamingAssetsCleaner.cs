using System.IO;
using Client.Build.Policy;
using UnityEngine;

namespace Client.Editor.Build
{
    /// <summary>
    /// 撤去前のビルドが作業コピーに残した Web UI の成果物を、焼く前に消す（配布物への混入を防ぐ）
    /// Removes Web UI output a pre-removal build left in the working copy before baking, so it never ships
    /// </summary>
    internal static class PreRemovalStreamingAssetsCleaner
    {
        // 撤去前は Web の dist をここへ書いていた。git 管理外なので作業コピーにだけ残る
        // Pre-removal builds wrote the Web dist here; it is untracked, so it lingers only in the working copy
        private static readonly string LeftoverDirectory = Path.Combine(Application.dataPath, "StreamingAssets", "WebUi");

        // 無ければ何もしない。消せなければ false を返し、焼く側がビルドを止める
        // Does nothing when absent; returns false when removal fails so the caller stops the build
        internal static bool RemoveLeftovers()
        {
            if (LeftoverDirectoryRemoval.TryRemove(LeftoverDirectory, out var removedAnything, out var hadMeta, out var failureReason))
            {
                if (removedAnything) Debug.Log($"[PreRemovalStreamingAssetsCleaner] 撤去前のビルドが残した Web UI の成果物を消しました: {LeftoverDirectory}（.meta {(hadMeta ? "あり" : "なし")}）");
                return true;
            }

            Debug.LogError($"[PreRemovalStreamingAssetsCleaner] 撤去前の Web UI の成果物を消せないためビルドを止めます。手で消してから焼き直してください: {LeftoverDirectory} ({failureReason})");
            return false;
        }
    }
}
