using System;
using UnityEngine;

namespace Game.MapGeneration.Transfer
{
    /// <summary>
    ///     生成ワールドだけが持つ転送値を、欠損した状態を作れない単位で保持する
    ///     Holds generated-world-only transfer values as one unit that cannot be constructed with missing data
    /// </summary>
    public sealed class GeneratedTerrainTransferPayload
    {
        public readonly TerrainOrigins Origins;
        public readonly string GenerationMasterFingerprint;
        public readonly string GeneratorVersion;
        public readonly string PlacementLedgerDigest;

        public GeneratedTerrainTransferPayload(
            TerrainOrigins origins, string generationMasterFingerprint, string generatorVersion, string placementLedgerDigest)
        {
            if (string.IsNullOrEmpty(generationMasterFingerprint))
                throw new ArgumentException("Generation master fingerprint must not be empty.", nameof(generationMasterFingerprint));
            if (string.IsNullOrEmpty(generatorVersion))
                throw new ArgumentException("Generator version must not be empty.", nameof(generatorVersion));
            if (string.IsNullOrEmpty(placementLedgerDigest))
                throw new ArgumentException("Placement ledger digest must not be empty.", nameof(placementLedgerDigest));

            Origins = origins;
            GenerationMasterFingerprint = generationMasterFingerprint;
            GeneratorVersion = generatorVersion;
            PlacementLedgerDigest = placementLedgerDigest;
        }

        // 原点はfloatでworld.jsonを往復し、注入時のG=NoiseOrigin-SceneOriginでも丸められる（数km地点のfloat刻みは約1mm）
        // 実際にずれるときは窓1枚ぶん（数百m以上）動くので、ハイトマップ1サンプル(約4m)の400分の1を同一とみなす閾値にする
        // The origins round-trip through world.json as floats and are rounded again by the injected G = NoiseOrigin - SceneOrigin (a float step is about 1mm several km out)
        // A real disagreement moves by a whole window (hundreds of metres), so the threshold sits at a four-hundredth of one heightmap sample (about 4m)
        public void ThrowIfOriginsDiffer(TerrainOrigins currentOrigins)
        {
            const float toleranceMeters = 0.01f;
            if (Vector2.Distance(currentOrigins.NoiseOrigin, Origins.NoiseOrigin) <= toleranceMeters &&
                Vector2.Distance(currentOrigins.SceneOrigin, Origins.SceneOrigin) <= toleranceMeters) return;

            throw new InvalidOperationException(
                $"Regenerated origins (noise {currentOrigins.NoiseOrigin}, scene {currentOrigins.SceneOrigin}) disagree with the transferred origins " +
                $"(noise {Origins.NoiseOrigin}, scene {Origins.SceneOrigin}).");
        }
    }
}
