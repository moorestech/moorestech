using Game.MapGeneration.Transfer;

namespace Server.Boot.Replay.World
{
    // 生成ワールドの world.json が再生に使えるかを調べる。TerrainTransferMetaReader が例外にする入力を、先に理由付きで弾くための唯一の判定
    // Checks whether a generated world's world.json is usable for replay; the single place that turns inputs TerrainTransferMetaReader would throw on into reasoned refusals
    public static class BugReportGeneratedWorldMetaCheck
    {
        // 箱側は worldId と台帳の指紋を出すためだけに読む。mapMode の判定は WorldMapMode.IsGenerated に揃え、呼び出し側が先に済ませる。問題なければ null
        // The bundle's side is read only to derive the worldId and the ledger digest; its mapMode is judged by WorldMapMode.IsGenerated in the caller beforehand. Returns null when usable
        public static string FindBundleMetaProblem(WorldMetaJson meta)
        {
            return FindRequiredKeyProblem(meta);
        }

        // 候補側は再生が TerrainTransferMetaReader でそのまま読む。あちらは mapMode を大文字小文字込みの完全一致で分岐するので、ここも完全一致で見る。問題なければ null
        // The candidate's side is read verbatim by TerrainTransferMetaReader during replay, which switches on the exact mapMode literal, so the exact literal is required here too. Returns null when usable
        public static string FindCandidateMetaProblem(WorldMetaJson meta)
        {
            if (meta.MapMode != WorldMapMode.Generated) return $"mapMode（{meta.MapMode}）が再生の読み手の受け付ける \"{WorldMapMode.Generated}\" と一致しない";
            return FindRequiredKeyProblem(meta);
        }

        // 指紋・台帳の指紋・生成器版・地形原点は TerrainTransferMetaReader が欠けると例外にする必須キー
        // The fingerprint, ledger digest, generator version and terrain origins are keys TerrainTransferMetaReader throws on when absent
        private static string FindRequiredKeyProblem(WorldMetaJson meta)
        {
            if (string.IsNullOrEmpty(meta.GenerationMasterFingerprint)) return "generationMasterFingerprint が無く worldId を導けない";
            if (string.IsNullOrEmpty(meta.PlacementLedgerDigest)) return "placementLedgerDigest が無く配置台帳を照合できない";
            if (meta.GeneratorVersion != WorldGeneratorVersion.Current) return $"generatorVersion（{meta.GeneratorVersion}）が現在のビルド（{WorldGeneratorVersion.Current}）と異なる";
            if (meta.TerrainNoiseOriginX == null || meta.TerrainNoiseOriginZ == null || meta.TerrainSceneOriginX == null || meta.TerrainSceneOriginZ == null)
                return "地形原点のキー（terrainNoiseOriginX/Z, terrainSceneOriginX/Z）が無い";
            return null;
        }
    }
}
