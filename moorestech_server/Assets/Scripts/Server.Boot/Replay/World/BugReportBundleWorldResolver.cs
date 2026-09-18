using System;
using System.Collections.Generic;
using System.IO;
using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using Game.Paths;
using UnityEngine;

namespace Server.Boot.Replay.World
{
    // 箱の world/ から再生に使うワールドを決める。manifest の worldDefinition 宣言で分岐し、full なら箱そのもの、生成ワールドなら world.json から同梱スナップショット／共有キャッシュを引き当てる（ADR 0064）
    // Decides the world used for replay from the bundle's world/ by the manifest's worldDefinition: the bundle itself for full, else for a generated world the bundled snapshot / shared cache located from world.json (ADR 0064)
    public static class BugReportBundleWorldResolver
    {
        public static BugReportBundleWorldResolution Resolve(string bundleDirectory, string serverDataDirectory)
        {
            var bundled = WorldDataDirectory.FromWorldRoot(Path.Combine(bundleDirectory, BugReportBundleLayout.WorldDirectoryName));
            // 記録時のワールドが無い箱をtemplateで代用するとinstanceIdがずれ、再生比較に偽の差分が出るので拒否する
            // Substituting the template for a bundle without its recorded world shifts instance ids and fabricates replay differences, so it is rejected
            if (!Directory.Exists(bundled.Root)) return BugReportBundleWorldResolution.Rejected($"バンドルに {BugReportBundleLayout.WorldDirectoryName}/ がありません（記録時のワールドが無いと再生は成立しません） bundle:{bundleDirectory}");
            var hasMapJson = File.Exists(bundled.MapJsonFilePath);

            // 推定に回すのはキーの無い旧版の箱だけ。旧版は常に全部入れていたので map.json の有無で推定し、推定に倒したことをログに残す。壊れた宣言は推定せず拒否する
            // Only an old box without the key falls back; old versions always shipped everything, so infer from map.json and log it. A broken declaration is refused, never inferred
            var declarationStatus = BugReportManifestWorldDefinitionReader.Read(bundleDirectory, out var definition, out var declarationReason);
            switch (declarationStatus)
            {
                case BugReportWorldDeclarationStatus.Malformed:
                    return BugReportBundleWorldResolution.Rejected($"箱の worldDefinition 宣言を読めず、記録時のワールドを決められません: {declarationReason} bundle:{bundleDirectory}");
                case BugReportWorldDeclarationStatus.LegacyUndeclared:
                    Debug.LogWarning($"箱が worldDefinition を宣言していないため map.json の有無で推定します: {declarationReason}");
                    return hasMapJson ? BugReportBundleWorldResolution.Resolved(bundled) : ResolveGenerated(bundled, bundleDirectory, serverDataDirectory);
                case BugReportWorldDeclarationStatus.Declared:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(declarationStatus), declarationStatus, "未知の宣言の読み取り結果");
            }

            // 宣言と中身が食い違う箱は、どちらを信じても記録時と別のワールドで再生しうるので拒否する
            // A box whose declaration contradicts its contents could replay a world other than the recording's whichever side is trusted, so it is rejected
            switch (definition)
            {
                case BugReportWorldDefinition.NotCaptured:
                    return BugReportBundleWorldResolution.Rejected($"記録時のワールドを取り込めなかった箱です（worldDefinition=not-captured。manifest の missing に理由があります） bundle:{bundleDirectory}");
                case BugReportWorldDefinition.Full:
                    if (hasMapJson) return BugReportBundleWorldResolution.Resolved(bundled);
                    return BugReportBundleWorldResolution.Rejected($"worldDefinition=full と宣言された箱に map.json がありません（コピーの途中で欠けた箱です） bundle:{bundleDirectory}");
                case BugReportWorldDefinition.GeneratedWorldJsonOnly:
                    if (hasMapJson) return BugReportBundleWorldResolution.Rejected($"worldDefinition=generated-world-json-only と宣言された箱に map.json があります（宣言と中身が食い違っています） bundle:{bundleDirectory}");
                    return ResolveGenerated(bundled, bundleDirectory, serverDataDirectory);
                default:
                    throw new ArgumentOutOfRangeException(nameof(definition), definition, "未知の worldDefinition");
            }
        }

        // map.json の無い箱は生成ワールドだけが成立する。判定は world.json の mapMode だけで行う
        // A bundle without map.json only works for a generated world, judged solely by world.json's mapMode
        private static BugReportBundleWorldResolution ResolveGenerated(WorldDataDirectory bundled, string bundleDirectory, string serverDataDirectory)
        {
            if (!File.Exists(bundled.WorldMetaFilePath)) return BugReportBundleWorldResolution.Rejected($"バンドルの {BugReportBundleLayout.WorldDirectoryName}/ に map.json も world.json もありません bundle:{bundleDirectory}");
            var meta = BugReportGeneratedWorldMetaCheck.ReadMeta(bundled.WorldMetaFilePath, out var readError);
            if (meta == null) return BugReportBundleWorldResolution.Rejected($"箱の world.json を読めません: {readError} path:{bundled.WorldMetaFilePath}");
            if (!WorldMapMode.IsGenerated(meta.MapMode))
            {
                return BugReportBundleWorldResolution.Rejected($"手作りワールド（mapMode={meta.MapMode}）なのに map.json が無く、記録時のワールドを引き当てられません bundle:{bundleDirectory}");
            }

            // worldId の導出と後段の読み手は必須キーが欠けると例外になる。再生ツールは同じ判定で例外でなく理由付きの拒否を返す
            // Deriving the worldId and the downstream reader throw on missing keys; the replay tool uses the same rule to return a reasoned rejection instead
            var bundleProblem = TerrainTransferMetaReader.DescribeGeneratedMetaProblem(meta);
            if (bundleProblem != null) return BugReportBundleWorldResolution.Rejected($"生成ワールドの world.json を使えません: {bundleProblem} path:{bundled.WorldMetaFilePath}");
            var worldId = TerrainTransferMetaReader.CalculateGeneratedWorldId(meta);

            // 探索順と置き場の妥当性は WorldSnapshotStore に委ねる。配置台帳が違えば別物なので次へ進まず拒否する
            // The search order and snapshot validity follow WorldSnapshotStore; a different placement ledger is a different world, so reject rather than move on
            var skipReasons = new List<string>();
            foreach (var candidate in WorldSnapshotStore.EnumerateSourceCandidates(serverDataDirectory, worldId))
            {
                if (!Directory.Exists(candidate.Root))
                {
                    skipReasons.Add($"root:{candidate.Root} が存在しない");
                    continue;
                }
                var snapshotProblem = WorldSnapshotStore.DescribeSnapshotProblem(candidate);
                if (snapshotProblem != null)
                {
                    LogAndRecordSkip($"root:{candidate.Root} に {snapshotProblem}");
                    continue;
                }

                // 再生はこの候補の world.json を読むので、箱側と同じ妥当性検査に加えて箱と同じワールドかを照合する
                // Replay reads this candidate's world.json, so beyond the bundle's validity checks it is matched against the box's world
                var candidateMeta = BugReportGeneratedWorldMetaCheck.ReadMeta(candidate.WorldMetaFilePath, out var candidateReadError);
                if (candidateMeta == null)
                {
                    LogAndRecordSkip($"root:{candidate.Root} の world.json を読めない: {candidateReadError}");
                    continue;
                }
                var candidateProblem = BugReportGeneratedWorldMetaCheck.FindCandidateProblem(candidate, candidateMeta, meta, worldId);
                if (candidateProblem != null)
                {
                    LogAndRecordSkip($"root:{candidate.Root} の {candidateProblem}");
                    continue;
                }
                if (candidateMeta.PlacementLedgerDigest != meta.PlacementLedgerDigest)
                {
                    return BugReportBundleWorldResolution.Rejected($"ワールド候補 {worldId} の placementLedgerDigest が箱の world.json と一致しません（箱:{meta.PlacementLedgerDigest} 候補:{candidateMeta.PlacementLedgerDigest}） root:{candidate.Root}{DescribeSkipped()}");
                }
                return BugReportBundleWorldResolution.Resolved(candidate);
            }
            return BugReportBundleWorldResolution.Rejected($"生成ワールド {worldId} が worldSnapshots にも共有キャッシュにもありません（同じコミットの配布ビルドの game/ を serverDataDirectory に指定すること） serverData:{serverDataDirectory}{DescribeSkipped()}");

            #region Internal

            void LogAndRecordSkip(string reason)
            {
                Debug.LogWarning($"ワールド候補を使いません: {reason}");
                skipReasons.Add(reason);
            }

            // 拒否理由に使わなかった候補の理由を連結する。エディタログにしか残らない真因を、結果本文にも表明する
            // Appends the reasons every skipped candidate was dropped, so the true cause is not left only in the editor log
            string DescribeSkipped()
            {
                return skipReasons.Count == 0 ? "" : $"（使わなかった候補: {string.Join("; ", skipReasons)}）";
            }

            #endregion
        }
    }
}
