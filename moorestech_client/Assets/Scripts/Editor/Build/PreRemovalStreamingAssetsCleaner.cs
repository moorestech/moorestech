using System;
using System.IO;
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
            var leftoverMeta = LeftoverDirectory + ".meta";
            var hasDirectory = Directory.Exists(LeftoverDirectory);
            var hasMeta = File.Exists(leftoverMeta);
            if (!hasDirectory && !hasMeta) return true;

            // 作業コピーのディスク IO は他プロセスのロック・権限で失敗しうる外部境界なので隔離する
            // Working-copy disk IO is an external boundary that can fail on locks or permissions, so isolate it
            try
            {
                if (hasDirectory) Directory.Delete(LeftoverDirectory, true);
                if (hasMeta) File.Delete(leftoverMeta);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"[PreRemovalStreamingAssetsCleaner] 撤去前の Web UI の成果物を消せないためビルドを止めます。手で消してから焼き直してください: {LeftoverDirectory} ({e.Message})");
                return false;
            }

            Debug.Log($"[PreRemovalStreamingAssetsCleaner] 撤去前のビルドが残した Web UI の成果物を消しました: {LeftoverDirectory}（.meta {(hasMeta ? "あり" : "なし")}）");
            return true;
        }
    }
}
