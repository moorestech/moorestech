using System;
using System.Collections.Generic;
using System.IO;
using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Newtonsoft.Json;
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
            if (!Directory.Exists(bundled.Root)) return BugReportBundleWorldResolution.Rejected($"バンドルに {BugReportBundleLayout.WorldDirectoryName}/ がありません（記録時のワールドが無いと再生は成立しません） bundle:{bundleDirectory}");
            var hasMapJson = File.Exists(bundled.MapJsonFilePath);

            // 宣言の無い箱は ADR 0064 以前の版か manifest の破損。旧版は常に全部入れていたので map.json の有無で推定し、推定に倒したことをログに残す
            // A box without a declaration predates ADR 0064 or has a broken manifest; old versions always shipped everything, so infer from map.json and log the fallback
            if (!BugReportManifestWorldDefinitionReader.TryRead(bundleDirectory, out var definition, out var undeclaredReason))
            {
                Debug.LogWarning($"箱の worldDefinition 宣言を使えないため map.json の有無で推定します: {undeclaredReason}");
                return hasMapJson ? BugReportBundleWorldResolution.Resolved(bundled) : ResolveGenerated(bundled, bundleDirectory, serverDataDirectory);
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
            var meta = ReadMeta(bundled.WorldMetaFilePath, out var readError);
            if (meta == null) return BugReportBundleWorldResolution.Rejected($"箱の world.json を読めません: {readError} path:{bundled.WorldMetaFilePath}");
            if (!WorldMapMode.IsGenerated(meta.MapMode))
            {
                return BugReportBundleWorldResolution.Rejected($"手作りワールド（mapMode={meta.MapMode}）なのに map.json が無く、記録時のワールドを引き当てられません bundle:{bundleDirectory}");
            }

            // worldId の導出と後段の読み手は必須キーが欠けると例外になる。再生ツールは例外でなく理由付きの拒否で返す
            // Deriving the worldId and the downstream reader throw on missing keys; the replay tool returns a reasoned rejection instead
            var bundleProblem = BugReportGeneratedWorldMetaCheck.FindBundleMetaProblem(meta);
            if (bundleProblem != null) return BugReportBundleWorldResolution.Rejected($"生成ワールドの world.json を使えません: {bundleProblem} path:{bundled.WorldMetaFilePath}");
            var worldId = WorldIdentity.CalculateGenerated(meta.Seed, meta.GenerationMasterFingerprint, meta.GeneratorVersion);

            // 同梱スナップショット → 共有キャッシュの順に探す。配置台帳が違えば別物なので次へ進まず拒否する
            // Search the bundled snapshot then the shared cache; a different placement ledger is a different world, so reject rather than move on
            var skipReasons = new List<string>();
            foreach (var candidate in new[] { WorldDataDirectory.ForBundledSnapshot(serverDataDirectory, worldId), WorldDataDirectory.ForWorldCacheWithoutCreating(worldId) })
            {
                if (!Directory.Exists(candidate.Root))
                {
                    skipReasons.Add($"root:{candidate.Root} が存在しない");
                    continue;
                }
                // 妥当性判定は WorldSnapshotStore と同じ基準（map.json・world.json・terrain/ の3点）に委ね、欠けた物を名指しする
                // Validity follows WorldSnapshotStore's criterion (map.json, world.json and terrain/), naming whichever piece is missing
                if (!WorldSnapshotStore.IsSnapshot(candidate))
                {
                    LogAndRecordSkip($"root:{candidate.Root} に {DescribeMissingSnapshotParts(candidate)}");
                    continue;
                }

                // 再生はこの候補の world.json を読むので、箱側と同じ妥当性検査を候補側にも掛ける
                // Replay reads this candidate's world.json, so the candidate gets the same validity checks as the bundle's side
                var candidateMeta = ReadMeta(candidate.WorldMetaFilePath, out var candidateReadError);
                if (candidateMeta == null)
                {
                    LogAndRecordSkip($"root:{candidate.Root} の world.json を読めない: {candidateReadError}");
                    continue;
                }
                var candidateProblem = BugReportGeneratedWorldMetaCheck.FindCandidateMetaProblem(candidateMeta);
                if (candidateProblem != null)
                {
                    LogAndRecordSkip($"root:{candidate.Root} の world.json を使えない: {candidateProblem}");
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

            string DescribeMissingSnapshotParts(WorldDataDirectory candidate)
            {
                var missing = new List<string>();
                if (!File.Exists(candidate.MapJsonFilePath)) missing.Add("map.json が無い");
                if (!File.Exists(candidate.WorldMetaFilePath)) missing.Add("world.json が無い");
                if (!Directory.Exists(candidate.TerrainDirectory)) missing.Add("terrain/ が無い");
                return string.Join("・", missing);
            }

            #endregion
        }

        // world.json は外部入力のファイルとJSON（ファイルI/O・権限とJSONパースの境界）。読めない理由を返し、呼び出し側が拒否理由やログに載せる
        // world.json is external file + JSON input (file I/O and JSON parse boundary); the reason is returned for the caller's rejection or log
        private static WorldMetaJson ReadMeta(string path, out string error)
        {
            error = null;
            try
            {
                var meta = JsonConvert.DeserializeObject<WorldMetaJson>(File.ReadAllText(path));
                if (meta == null) error = "world.json が空です";
                return meta;
            }
            catch (JsonException e)
            {
                error = e.Message;
                return null;
            }
            catch (IOException e)
            {
                error = e.Message;
                return null;
            }
            catch (UnauthorizedAccessException e)
            {
                error = e.Message;
                return null;
            }
        }
    }
}
