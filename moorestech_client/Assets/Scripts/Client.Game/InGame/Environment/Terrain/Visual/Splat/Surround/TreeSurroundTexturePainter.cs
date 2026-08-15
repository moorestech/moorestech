using System;
using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Visual.Splat.Surround
{
    /// <summary>
    ///     木の根元まわりのalphamapを樹種ごとのレイヤーへ寄せる。移植元 TreePlacementGenerator.ApplyTextureModification(:636-707)。
    ///     畳み方は岩の裸地(SurroundBlendWriter)とは別物で、元の合計を掛けず再正規化もせず、ガウシアン1本だけで減衰する。
    ///     樹種はprototypeIndexではなくmapObjectGuidで引く。転送レイアウトが運ぶのはguidだけだから
    ///     Pulls the alphamap under a tree's root onto that species' layer; ported from TreePlacementGenerator.ApplyTextureModification (:636-707).
    ///     The fold differs from the rocks' bare ground (SurroundBlendWriter): it neither multiplies by the original total nor
    ///     renormalizes, fading by a single Gaussian. Species are keyed by mapObjectGuid rather than prototypeIndex, since a guid is all the transferred layout carries
    /// </summary>
    public static class TreeSurroundTexturePainter
    {
        // guid → (レイヤーアドレス, 重み, 幅)。TreeHeightModifier.BuildGuidModMapと同じ規約で最初の出現が勝つ
        // guid to (layer address, weight, width) under BuildGuidModMap's rule, where the first occurrence wins
        public static Dictionary<string, (string layerAddress, float weight, float width)> BuildGuidSurroundMap(
            BiomePlacementHelper helper, BiomeType[] biomeTypes)
        {
            var surroundParamsByGuid = new Dictionary<string, (string, float, float)>();
            foreach (var biome in biomeTypes)
            {
                var treePlacement = helper.GetTreePlacementConfig(biome);
                if (treePlacement?.prototypes == null) continue;

                // 塗らないプロトタイプも載せる。最初の出現が勝つ規約は「塗るかどうか」より前に決まるため
                // Non-painting prototypes are mapped too: the first-occurrence rule is settled before painting is
                foreach (var entry in treePlacement.prototypes)
                {
                    if (entry == null || entry.disabled || entry.mapObjectGuids == null) continue;

                    foreach (var mapObjectGuid in entry.mapObjectGuids)
                    {
                        if (string.IsNullOrEmpty(mapObjectGuid) || surroundParamsByGuid.ContainsKey(mapObjectGuid)) continue;
                        surroundParamsByGuid[mapObjectGuid] =
                            (entry.surroundLayerAddressablePath, entry.surroundLayerWeight, entry.surroundLayerWidth);
                    }
                }
            }

            return surroundParamsByGuid;
        }

        // splatmapの列は塗る樹種のぶんだけ要る。重み0や未設定のアドレスまで登録すると使われない列がTerrainLayerごと増える
        // The splatmap needs a column per painting species; registering unset or zero-weight addresses would add unused columns and TerrainLayers
        public static List<string> LayerAddresses(
            IReadOnlyDictionary<string, (string layerAddress, float weight, float width)> surroundParamsByGuid)
        {
            var layerAddresses = new List<string>();
            foreach (var surroundParams in surroundParamsByGuid.Values)
                if (Paints(surroundParams))
                    layerAddresses.Add(surroundParams.layerAddress);

            return layerAddresses;
        }

        // 隣タイルの木の根元もこちらへ伸びる。切り出しhaloがこの距離を下回るとタイル境界で根元の塗りが直線に切れる
        // A tree's root reaches in from the neighbouring tile too; a slice halo below this distance breaks the root patch in a straight line at the seam
        public static float MaxReach(
            IReadOnlyDictionary<string, (string layerAddress, float weight, float width)> surroundParamsByGuid)
        {
            var reach = 0f;
            foreach (var surroundParams in surroundParamsByGuid.Values)
                if (Paints(surroundParams))
                    reach = Mathf.Max(reach, surroundParams.width);

            return reach;
        }

        public static void Apply(
            float[,,] alphamap, TerrainGenerationConfig config, SplatLayerTable layerTable,
            IReadOnlyDictionary<string, (string layerAddress, float weight, float width)> surroundParamsByGuid,
            IReadOnlyList<MapObjectLayoutMessagePack> treeObjects)
        {
            var alphaResolution = alphamap.GetLength(0);

            foreach (var treeObject in treeObjects)
            {
                // 岩・鉱脈のguidはマップに載らない。木でも未設定・重み0のプロトタイプはここで抜ける
                // Rock and vein guids never enter the map, and an unset or zero-weight tree prototype leaves here too
                if (!surroundParamsByGuid.TryGetValue(treeObject.MapObjectGuid, out var surroundParams)) continue;
                if (!Paints(surroundParams)) continue;

                // 幅0はsigma0でガウシアンがNaNになり、alphamap全体へ伝播する。黙って飛ばさずデータの穴として落とす
                // A zero width makes sigma zero and the Gaussian NaN, which spreads across the alphamap; it fails as a data gap rather than being skipped
                if (surroundParams.width <= 0f)
                    throw new InvalidOperationException(
                        $"[TreeSurroundTexturePainter] MapObject {treeObject.MapObjectGuid} carries surroundLayerWeight {surroundParams.weight} with a surroundLayerWidth of {surroundParams.width}.");

                var layerIndex = layerTable.LayerIndexByAddress[surroundParams.layerAddress];

                // 半径も中心もalphamapの実寸基準。移植元はheightmap解像度を渡してclampで潰していたので、そこだけ正した
                // Both radius and centre use the alphamap's own resolution; the source passed the heightmap's and hid the gap behind a clamp
                var radiusInPixels = surroundParams.width / config.terrainWidth * (alphaResolution - 1);
                var scanRadius = Mathf.CeilToInt(radiusInPixels);
                var centerX = Mathf.RoundToInt(treeObject.X / config.terrainWidth * (alphaResolution - 1));
                var centerZ = Mathf.RoundToInt(treeObject.Z / config.terrainLength * (alphaResolution - 1));

                for (var offsetZ = -scanRadius; offsetZ <= scanRadius; offsetZ++)
                for (var offsetX = -scanRadius; offsetX <= scanRadius; offsetX++)
                {
                    var pixelX = centerX + offsetX;
                    var pixelZ = centerZ + offsetZ;
                    if (pixelX < 0 || alphaResolution <= pixelX || pixelZ < 0 || alphaResolution <= pixelZ) continue;

                    var distance = Mathf.Sqrt(offsetX * offsetX + offsetZ * offsetZ);
                    if (radiusInPixels < distance) continue;

                    var sigma = radiusInPixels / 3f;
                    var falloff = Mathf.Exp(-(distance * distance) / (2f * sigma * sigma));
                    BlendWithoutTotal(alphamap, pixelZ, pixelX, layerIndex, surroundParams.weight * falloff);
                }
            }
        }

        // 岩のSurroundBlendWriterは元の合計を掛けて足すが、木は掛けない。流用すると根元の塗り強度だけが静かに変わる
        // The rocks' SurroundBlendWriter adds the blended share of the original total; a tree does not, and reusing it would quietly change the root's strength
        private static void BlendWithoutTotal(float[,,] alphamap, int pixelZ, int pixelX, int layerIndex, float blend)
        {
            var layerCount = alphamap.GetLength(2);
            var remaining = 1f - blend;

            for (var layer = 0; layer < layerCount; layer++)
            {
                if (layer == layerIndex) continue;
                alphamap[pixelZ, pixelX, layer] *= remaining;
            }

            alphamap[pixelZ, pixelX, layerIndex] = alphamap[pixelZ, pixelX, layerIndex] * remaining + blend;
        }

        // 移植元の `layer == null || weight <= 0f` と同じ足切り。列の確保・haloの幅・実際の塗りが同じ条件で揃う
        // The same cutoff as the source's `layer == null || weight <= 0f`, keeping the reserved columns, the halo and the painting on one condition
        private static bool Paints((string layerAddress, float weight, float width) surroundParams)
        {
            return !string.IsNullOrEmpty(surroundParams.layerAddress) && 0f < surroundParams.weight;
        }
    }
}
