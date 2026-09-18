using System;
using System.IO;
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
            public ResolvedWorld(WorldDataDirectory world) { World = world; }
            public override BugReportBundleWorldOutcome Outcome => BugReportBundleWorldOutcome.Resolved;
        }

        public sealed class RejectedWorld : BugReportBundleWorldResolution
        {
            public readonly string Reason;
            public RejectedWorld(string reason) { Reason = reason; }
            public override BugReportBundleWorldOutcome Outcome => BugReportBundleWorldOutcome.Rejected;
        }

        public static BugReportBundleWorldResolution Resolved(WorldDataDirectory world) { return new ResolvedWorld(world); }
        public static BugReportBundleWorldResolution Rejected(string reason) { return new RejectedWorld(reason); }
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
            if (!string.Equals(meta.MapMode, WorldMapMode.Generated, StringComparison.OrdinalIgnoreCase))
            {
                return BugReportBundleWorldResolution.Rejected($"手作りワールド（mapMode={meta.MapMode}）なのに map.json が無く、記録時のワールドを引き当てられません bundle:{bundleDirectory}");
            }

            // worldId の導出は指紋必須で、欠けると例外になる。再生ツールは例外でなく理由付きの拒否で返す
            // Deriving the worldId requires the fingerprint and throws without it; the replay tool returns a reasoned rejection instead
            if (string.IsNullOrEmpty(meta.GenerationMasterFingerprint)) return BugReportBundleWorldResolution.Rejected($"生成ワールドの world.json に generationMasterFingerprint が無く worldId を導けません path:{bundled.WorldMetaFilePath}");
            var worldId = WorldIdentity.CalculateGenerated(meta.Seed, meta.GenerationMasterFingerprint, meta.GeneratorVersion);

            // 同梱スナップショット → 共有キャッシュの順に探す。配置台帳が違えば別物なので次へ進まず拒否する
            // Search the bundled snapshot then the shared cache; a different placement ledger is a different world, so reject rather than move on
            foreach (var candidate in new[] { WorldDataDirectory.ForBundledSnapshot(serverDataDirectory, worldId), WorldDataDirectory.ForWorldCache(worldId) })
            {
                if (!Directory.Exists(candidate.Root)) continue;
                if (!File.Exists(candidate.MapJsonFilePath) || !File.Exists(candidate.WorldMetaFilePath))
                {
                    Debug.LogWarning($"ワールド候補に map.json か world.json が欠けているため使いません root:{candidate.Root}");
                    continue;
                }

                var candidateMeta = ReadMeta(candidate.WorldMetaFilePath, out var candidateReadError);
                if (candidateMeta == null)
                {
                    Debug.LogWarning($"ワールド候補の world.json を読めないため使いません: {candidateReadError} root:{candidate.Root}");
                    continue;
                }
                if (candidateMeta.PlacementLedgerDigest != meta.PlacementLedgerDigest)
                {
                    return BugReportBundleWorldResolution.Rejected($"ワールド候補 {worldId} の placementLedgerDigest が箱の world.json と一致しません（箱:{meta.PlacementLedgerDigest} 候補:{candidateMeta.PlacementLedgerDigest}） root:{candidate.Root}");
                }
                return BugReportBundleWorldResolution.Resolved(candidate);
            }
            return BugReportBundleWorldResolution.Rejected($"生成ワールド {worldId} が worldSnapshots にも共有キャッシュにもありません（同じコミットの配布ビルドの game/ を serverDataDirectory に指定すること） serverData:{serverDataDirectory}");
        }

        // world.json は外部入力のファイルとJSON（ファイルI/OとJSONパースの境界）。読めない理由を返し、呼び出し側が拒否理由やログに載せる
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
        }
    }
}
