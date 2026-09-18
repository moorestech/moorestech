using System;
using System.Collections.Generic;
using System.IO;
using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Newtonsoft.Json;
using UnityEngine;

namespace Server.Boot.Replay
{
    public enum BugReportBundleWorldOutcome
    {
        Resolved,
        Rejected,
    }

    // 解決か拒否かは列挙型一本で判別する。World と Reason を null で使い分けない
    // Resolved vs rejected is told by the enum alone; World and Reason are never distinguished by null
    public abstract class BugReportBundleWorldResolution
    {
        public abstract BugReportBundleWorldOutcome Outcome { get; }

        public sealed class ResolvedWorld : BugReportBundleWorldResolution
        {
            public readonly WorldDataDirectory World;
            internal ResolvedWorld(WorldDataDirectory world) { World = world; }
            public override BugReportBundleWorldOutcome Outcome => BugReportBundleWorldOutcome.Resolved;
        }

        public sealed class RejectedWorld : BugReportBundleWorldResolution
        {
            public readonly string Reason;
            internal RejectedWorld(string reason) { Reason = reason; }
            public override BugReportBundleWorldOutcome Outcome => BugReportBundleWorldOutcome.Rejected;
        }

        // 外から不整合な結果を組み立てられないよう、生成はresolverからのfactoryだけに絞る
        // Construction stays behind these internal factories so only the resolver can assemble a result
        internal static BugReportBundleWorldResolution Resolved(WorldDataDirectory world) { return new ResolvedWorld(world); }
        internal static BugReportBundleWorldResolution Rejected(string reason) { return new RejectedWorld(reason); }
    }

    // 箱の world/ から再生に使うワールドを決める。map.json があればその箱、無ければ生成ワールドに限り world.json から同梱スナップショット／共有キャッシュを引き当てる（ADR 0064）
    // Decides the world used for replay from the bundle's world/: the bundle itself when map.json is present, else for a generated world the bundled snapshot / shared cache located from world.json (ADR 0064)
    public static class BugReportBundleWorldResolver
    {
        public static BugReportBundleWorldResolution Resolve(string bundleDirectory, string serverDataDirectory)
        {
            // 記録時のワールドそのもの（map.json）を持つ箱はそれを使う。template で代用すると instanceId がずれて偽の差分になる
            // A bundle carrying the recording's own world (map.json) uses it; substituting the template shifts instance ids and fabricates differences
            var bundled = WorldDataDirectory.FromWorldRoot(Path.Combine(bundleDirectory, BugReportBundleLayout.WorldDirectoryName));
            if (!Directory.Exists(bundled.Root)) return BugReportBundleWorldResolution.Rejected($"バンドルに {BugReportBundleLayout.WorldDirectoryName}/ がありません（記録時のワールドが無いと再生は成立しません） bundle:{bundleDirectory}");
            if (File.Exists(bundled.MapJsonFilePath)) return BugReportBundleWorldResolution.Resolved(bundled);

            // map.json が無い箱は生成ワールドだけが成立する。判定は world.json の mapMode だけで行う
            // A bundle without map.json only works for a generated world, judged solely by world.json's mapMode
            if (!File.Exists(bundled.WorldMetaFilePath)) return BugReportBundleWorldResolution.Rejected($"バンドルの {BugReportBundleLayout.WorldDirectoryName}/ に map.json も world.json もありません bundle:{bundleDirectory}");
            var meta = ReadMeta(bundled.WorldMetaFilePath, out var readError);
            if (meta == null) return BugReportBundleWorldResolution.Rejected($"箱の world.json を読めません: {readError} path:{bundled.WorldMetaFilePath}");
            if (!WorldMapMode.IsGenerated(meta.MapMode))
            {
                return BugReportBundleWorldResolution.Rejected($"手作りワールド（mapMode={meta.MapMode}）なのに map.json が無く、記録時のワールドを引き当てられません bundle:{bundleDirectory}");
            }

            // worldId の導出は指紋必須で、欠けると例外になる。再生ツールは例外でなく理由付きの拒否で返す
            // Deriving the worldId requires the fingerprint and throws without it; the replay tool returns a reasoned rejection instead
            if (string.IsNullOrEmpty(meta.GenerationMasterFingerprint)) return BugReportBundleWorldResolution.Rejected($"生成ワールドの world.json に generationMasterFingerprint が無く worldId を導けません path:{bundled.WorldMetaFilePath}");
            // 台帳の指紋・生成器版・地形原点はTerrainTransferMetaReaderが例外にする必須キー。resolver側で先に理由付き拒否する
            // The ledger digest, generator version and terrain origins are keys TerrainTransferMetaReader throws on when missing; the resolver rejects them with a reason first
            if (string.IsNullOrEmpty(meta.PlacementLedgerDigest)) return BugReportBundleWorldResolution.Rejected($"生成ワールドの world.json に placementLedgerDigest が無く配置台帳を照合できません path:{bundled.WorldMetaFilePath}");
            if (meta.GeneratorVersion != WorldGeneratorVersion.Current) return BugReportBundleWorldResolution.Rejected($"生成ワールドの world.json の generatorVersion（{meta.GeneratorVersion}）が現在のビルド（{WorldGeneratorVersion.Current}）と異なり地形を引き当てられません path:{bundled.WorldMetaFilePath}");
            if (meta.TerrainNoiseOriginX == null || meta.TerrainNoiseOriginZ == null || meta.TerrainSceneOriginX == null || meta.TerrainSceneOriginZ == null)
                return BugReportBundleWorldResolution.Rejected($"生成ワールドの world.json に地形原点のキー（terrainNoiseOriginX/Z, terrainSceneOriginX/Z）が無く地形を引き当てられません path:{bundled.WorldMetaFilePath}");
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
                // 妥当性判定は WorldSnapshotStore と同じ基準（map.json・world.json・terrain/ の3点）に委ねる
                // Validity is judged by the same criterion as WorldSnapshotStore (map.json, world.json and terrain/ all present)
                if (!WorldSnapshotStore.IsSnapshot(candidate))
                {
                    LogAndRecordSkip($"root:{candidate.Root} は map.json / world.json / terrain/ のいずれかが欠けている");
                    continue;
                }

                var candidateMeta = ReadMeta(candidate.WorldMetaFilePath, out var candidateReadError);
                if (candidateMeta == null)
                {
                    LogAndRecordSkip($"root:{candidate.Root} の world.json を読めない: {candidateReadError}");
                    continue;
                }
                if (string.IsNullOrEmpty(candidateMeta.PlacementLedgerDigest))
                {
                    LogAndRecordSkip($"root:{candidate.Root} の world.json に placementLedgerDigest が無い");
                    continue;
                }
                if (candidateMeta.GeneratorVersion != WorldGeneratorVersion.Current)
                {
                    LogAndRecordSkip($"root:{candidate.Root} の world.json の generatorVersion（{candidateMeta.GeneratorVersion}）が現在のビルド（{WorldGeneratorVersion.Current}）と異なる");
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

            // world.json は外部入力のファイルとJSON（ファイルI/O・権限とJSONパースの境界）。読めない理由を返し、呼び出し側が拒否理由やログに載せる
            // world.json is external file + JSON input (file I/O and JSON parse boundary); the reason is returned for the caller's rejection or log
            WorldMetaJson ReadMeta(string path, out string error)
            {
                error = null;
                try
                {
                    var candidateMetaResult = JsonConvert.DeserializeObject<WorldMetaJson>(File.ReadAllText(path));
                    if (candidateMetaResult == null) error = "world.json が空です";
                    return candidateMetaResult;
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

            #endregion
        }
    }
}
