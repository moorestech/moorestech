using System;
using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Biomes;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Visual.Splat.Surround
{
    /// <summary>
    ///     樹種guidごとの根元設定と、そこから導いたsplatmapの列アドレス・切り出しhaloを1つに束ねる。
    ///     列を確保するSplatLayerTable.Buildも実際に塗るTreeSurroundTexturePainter.Applyもこの型しか受け取らないので、
    ///     列だけ別の樹種集合から作って塗りと食い違わせる（実行時のKeyNotFoundException）ことが型の上で書けない
    ///     Binds the per-species root settings to the splatmap columns and slice halo derived from them.
    ///     SplatLayerTable.Build, which reserves the columns, and TreeSurroundTexturePainter.Apply, which paints, both take
    ///     only this type, so deriving the columns from another species set than the painting one is not expressible
    /// </summary>
    public class TreeSurroundSpeciesTable
    {
        // splatmapに要る列は塗る樹種のぶんだけ。重み0や未設定のアドレスまで登録すると使われない列がTerrainLayerごと増える
        // The splatmap needs one column per painting species; unset or zero-weight addresses would add unused columns and TerrainLayers
        public readonly IReadOnlyList<string> LayerAddresses;

        // 隣タイルの木の根元もこちらへ伸びる。切り出しhaloがこの距離を下回るとタイル境界で根元の塗りが直線に切れる
        // A root reaches in from the neighbouring tile too, so a slice halo below this distance breaks the patch in a straight line at the seam
        public readonly float MaxReach;

        private readonly IReadOnlyDictionary<string, (string layerAddress, float weight, float width)> _surroundParamsByGuid;

        private TreeSurroundSpeciesTable(
            IReadOnlyDictionary<string, (string layerAddress, float weight, float width)> surroundParamsByGuid,
            IReadOnlyList<string> layerAddresses, float maxReach)
        {
            _surroundParamsByGuid = surroundParamsByGuid;
            LayerAddresses = layerAddresses;
            MaxReach = maxReach;
        }

        // guid → (レイヤーアドレス, 重み, 幅)。どのentryが勝つかはBiomePlacementHelperが唯一決める
        // Projects (layer address, weight, width) out of the entries BiomePlacementHelper alone picks per guid
        public static TreeSurroundSpeciesTable Build(BiomePlacementHelper helper, BiomeType[] biomeTypes)
        {
            // 塗らないプロトタイプも載せる。最初の出現が勝つ規約は「塗るかどうか」より前に決まるため
            // Non-painting prototypes are mapped too: the first-occurrence rule is settled before painting is
            var surroundParamsByGuid = new Dictionary<string, (string layerAddress, float weight, float width)>();
            foreach (var pair in helper.BuildFirstTreePrototypeByGuid(biomeTypes))
            {
                var entry = pair.Value;
                surroundParamsByGuid[pair.Key] =
                    (entry.surroundLayerAddressablePath, entry.surroundLayerWeight, entry.surroundLayerWidth);
            }

            // 列とhaloは組み上がったマップからその場で導く。導出をこの1箇所に閉じるのがこの型の存在理由
            // The columns and the halo are derived from the finished map right here; closing that derivation into one place is why this type exists
            var layerAddresses = new List<string>();
            var maxReach = 0f;
            foreach (var pair in surroundParamsByGuid)
            {
                var surroundParams = pair.Value;
                if (!Paints(surroundParams)) continue;

                // 幅0はsigma0でガウシアンがNaNになりalphamap全体へ伝播する。塗る前のここで確定的に落とす
                // A zero width makes sigma zero and the Gaussian NaN across the whole alphamap, so it fails deterministically here before any painting
                if (surroundParams.width <= 0f)
                    throw new InvalidOperationException(
                        $"[TreeSurroundSpeciesTable] MapObject {pair.Key} carries surroundLayerWeight {surroundParams.weight} with a surroundLayerWidth of {surroundParams.width}.");

                layerAddresses.Add(surroundParams.layerAddress);
                maxReach = Mathf.Max(maxReach, surroundParams.width);
            }

            return new TreeSurroundSpeciesTable(surroundParamsByGuid, layerAddresses, maxReach);
        }

        // 塗る樹種だけを返す。移植元の `layer == null || weight <= 0f` と同じ足切りを列・halo・塗りが共有する
        // Returns the painting species only, sharing the source's `layer == null || weight <= 0f` cutoff with the columns and the halo
        public bool TryGetPaintingParams(
            string mapObjectGuid, out (string layerAddress, float weight, float width) surroundParams)
        {
            if (!_surroundParamsByGuid.TryGetValue(mapObjectGuid, out surroundParams)) return false;

            return Paints(surroundParams);
        }

        private static bool Paints((string layerAddress, float weight, float width) surroundParams)
        {
            return !string.IsNullOrEmpty(surroundParams.layerAddress) && 0f < surroundParams.weight;
        }
    }
}
