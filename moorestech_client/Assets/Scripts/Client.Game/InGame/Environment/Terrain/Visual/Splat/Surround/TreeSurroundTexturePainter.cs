using System;
using System.Collections.Generic;
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
        public static void Apply(
            float[,,] alphamap, TerrainGenerationConfig config, SplatLayerTable layerTable,
            TreeSurroundSpeciesTable speciesTable, IReadOnlyList<MapObjectLayoutMessagePack> treeObjects)
        {
            var alphaResolution = alphamap.GetLength(0);

            foreach (var treeObject in treeObjects)
            {
                // 岩・鉱脈のguidは樹種テーブルに載らない。木でも未設定・重み0のプロトタイプはここで抜ける
                // Rock and vein guids never enter the species table, and an unset or zero-weight tree prototype leaves here too
                if (!speciesTable.TryGetPaintingParams(treeObject.MapObjectGuid, out var surroundParams)) continue;

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
    }
}
