using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Paths;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.Game.InGame.BugReport.BuildOrigin
{
    // ビルド時のリポジトリ状態から build-info.json（shared-contracts §1）の中身を組み、strict なら出所を偽る焼き込みを拒む
    // Editorアセンブリを参照できないテストからも検証できるよう、焼く側の判断は BuildInfoWriter ではなくここに置く
    // Composes build-info.json (shared-contracts §1) from the repository state at build time, refusing an origin-misreporting bake when strict
    // The baking decisions live here rather than in BuildInfoWriter so tests that cannot reference the Editor assembly can verify them
    public static class BuildInfoComposer
    {
        public const string SteamBuildLabelEnvKey = "MOORESTECH_STEAM_BUILD_LABEL";

        // buildFailureReason は strict で焼き込みを拒むときだけ非null。非strictは同じ理由を警告ログへ出して焼き続ける
        // buildFailureReason is non-null only when strict refuses the bake; non-strict logs the same reasons as warnings and keeps baking
        public static string Compose(
            RepositoryProbeResult repo,
            RepositoryProbeResult masterData,
            string pinnedMasterDataCommit,
            string pinUnreadableReason,
            string steamBuildLabel,
            DateTime builtAtUtc,
            string target,
            bool isStrictBundling,
            out string buildFailureReason)
        {
            var problems = CollectProblems();
            buildFailureReason = null;
            if (problems.Count != 0)
            {
                var joined = string.Join(" / ", problems);
                if (isStrictBundling) buildFailureReason = joined;
                else Debug.LogWarning($"build-info.json の出所を完全には保証できませんがCI互換（非strict）のため焼き込みを続けます: {joined}");
            }

            // 取れなかった状態は ""・false で焼かずnullで焼く。false は「クリーンな作業ツリー」という実値に化ける（F02）
            // An unavailable state is baked as null, not "" or false; false would pose as a real clean working tree (F02)
            var info = new JObject
            {
                ["commit"] = repo.State?.Commit,
                ["branch"] = repo.State?.Branch,
                ["dirty"] = repo.State?.Dirty,
                ["masterDataCommit"] = masterData.State?.Commit,
                ["masterDirty"] = masterData.State?.Dirty,
                ["steamBuildLabel"] = steamBuildLabel,
                ["builtAt"] = builtAtUtc.ToString(BugReportBundleLayout.Utc8601Format, CultureInfo.InvariantCulture),
                ["target"] = target,
            };
            return info.ToString(Formatting.Indented);

            #region Internal

            List<string> CollectProblems()
            {
                var found = new List<string>();
                if (repo.Error != null) found.Add($"本repoの状態を取れない: {repo.Error}");
                if (masterData.Error != null) found.Add($"master data repoの状態を取れない: {masterData.Error}");

                // ピンと実チェックアウトがずれた成果物は「どのマスタで焼いたか」の突き合わせが成立しない（D-6）
                // An artifact whose pin drifted from the checkout cannot be matched to the master data it was built with (D-6)
                if (pinnedMasterDataCommit == null) found.Add($"master data のピンコミットを読めない: {pinUnreadableReason}");
                else if (masterData.State != null && masterData.State.Commit != pinnedMasterDataCommit)
                    found.Add($"master data のピン({pinnedMasterDataCommit})と実HEAD({masterData.State.Commit})が一致しない");
                return found;
            }

            #endregion
        }
    }
}
