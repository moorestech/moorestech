using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Visual.Splat.Surround
{
    /// <summary>
    ///     岩をクラスタ単位にまとめ、その周りのalphamapを裸地レイヤーへ寄せる。移植元 TerrainGenerator.cs:1513-1571
    ///     と ResolveSurroundConfig(:1714-1743) の移植で、クラスタ内外の描画本体は2つの painter が持つ。
    ///     受け取る岩はタイルローカル座標で、クラスタ重心も TileMapObjectSlicer が同じ原点へ寄せ済み
    ///     Groups rocks by cluster and pulls the alphamap around them onto a bare-ground layer; ported from the source's
    ///     TerrainGenerator.cs:1513-1571 plus ResolveSurroundConfig (:1714-1743), with the two painters owning the drawing.
    ///     The rocks arrive in tile-local coordinates, their cluster centroids rebased onto the same origin by TileMapObjectSlicer
    /// </summary>
    public static class ObjectSurroundTexturePainter
    {
        // 移植元は TerrainLayer.name の一致でMudを探した。アドレス末尾がアセット名なので一致条件は同値
        // The source matched Mud on TerrainLayer.name; an address ends in the asset name, so the condition is equivalent
        private const string FallbackLayerAddressKeyword = "Mud";

        // 隣タイルの岩からも裸地は伸びる。切り出しhaloがこの距離を下回るとタイル境界で裸地が直線に切れる
        // Bare ground reaches in from neighbouring tiles too; a slice halo below this distance breaks it in a straight line at the seam
        public static float MaxReach(
            SurroundTextureConfig[] surroundConfigs, IReadOnlyList<MapObjectLayoutMessagePack> mapObjects)
        {
            var maxHorizontalScale = 0f;
            foreach (var mapObject in mapObjects)
                maxHorizontalScale = Mathf.Max(maxHorizontalScale, (mapObject.ScaleX + mapObject.ScaleZ) * 0.5f);

            var reach = 0f;
            foreach (var surroundConfig in surroundConfigs)
            {
                if (!surroundConfig.enabled) continue;

                reach = Mathf.Max(
                    reach, surroundConfig.transitionRadius + surroundConfig.rockMeshBaseSize * maxHorizontalScale);
                reach = Mathf.Max(reach, surroundConfig.singleRockRadius);
            }

            return reach;
        }

        public static void Apply(
            float[,,] alphamap, TerrainGenerationConfig config, SplatLayerTable layerTable,
            SurroundTextureConfig[] surroundConfigs, float[,] biomeWeights, int biomeCount,
            float[,] heights, IReadOnlyList<MapObjectLayoutMessagePack> stoneObjects)
        {
            var fallbackLayerIndex = FindFallbackLayerIndex();

            var clusterGroups = new Dictionary<int, List<MapObjectLayoutMessagePack>>();
            var nonClusterObjects = new List<MapObjectLayoutMessagePack>();
            GroupByCluster();

            // クラスタは重心のバイオームで設定を1つに決める。メンバーごとに引くとクラスタが境界を跨いだとき裸地が割れる
            // A cluster resolves one config at its centroid; resolving per member would split the bare ground where a cluster crosses a boundary
            foreach (var clusterGroup in clusterGroups)
            {
                var members = clusterGroup.Value;
                var surroundConfig = ResolveSurroundConfig(members[0].ClusterCenterX, members[0].ClusterCenterZ);
                if (!TryResolveLayerIndex(surroundConfig, out var layerIndex)) continue;

                SurroundClusterPainter.Paint(alphamap, config, surroundConfig, layerIndex, members, heights);
            }

            foreach (var stoneObject in nonClusterObjects)
            {
                var surroundConfig = ResolveSurroundConfig(stoneObject.X, stoneObject.Z);
                if (!TryResolveLayerIndex(surroundConfig, out var layerIndex)) continue;

                SurroundSingleRockPainter.Paint(alphamap, config, surroundConfig, layerIndex, stoneObject);
            }

            #region Internal

            void GroupByCluster()
            {
                foreach (var stoneObject in stoneObjects)
                {
                    if (stoneObject.ClusterId < 0)
                    {
                        nonClusterObjects.Add(stoneObject);
                        continue;
                    }

                    if (!clusterGroups.TryGetValue(stoneObject.ClusterId, out var members))
                    {
                        members = new List<MapObjectLayoutMessagePack>();
                        clusterGroups[stoneObject.ClusterId] = members;
                    }

                    members.Add(stoneObject);
                }
            }

            int FindFallbackLayerIndex()
            {
                for (var index = 0; index < layerTable.OrderedLayerAddresses.Count; index++)
                    if (layerTable.OrderedLayerAddresses[index].Contains(FallbackLayerAddressKeyword))
                        return index;

                // v8は全バイオームがsurroundLayer未設定でフォールバック頼り。Mudが無いと岩周辺が一切裸地化しない
                // Every v8 biome leaves surroundLayer unset and leans on the fallback; with no Mud no rock gets bare ground at all
                Debug.Log(
                    $"[ObjectSurroundTexturePainter] No splatmap layer address contains '{FallbackLayerAddressKeyword}'; rocks without an explicit surroundLayer stay untouched.");
                return -1;
            }

            // surroundLayer未設定ならMudへ倒す。移植元の `stCfg.surroundLayer ?? fallbackMudLayer` と同じ順序
            // An unset surroundLayer falls back to Mud, in the same order as the source's `stCfg.surroundLayer ?? fallbackMudLayer`
            bool TryResolveLayerIndex(SurroundTextureConfig surroundConfig, out int layerIndex)
            {
                layerIndex = string.IsNullOrEmpty(surroundConfig.surroundLayerAddressablePath)
                    ? fallbackLayerIndex
                    : layerTable.LayerIndexByAddress[surroundConfig.surroundLayerAddressablePath];

                return 0 <= layerIndex && surroundConfig.enabled;
            }

            // 分類重みの勝者バイオームで設定を引く。列オフセット+2はOcean/Beach列ぶんで移植元と同一
            // Picks the config by the classified weights' winning biome; the +2 column offset covers Ocean/Beach as in the source
            SurroundTextureConfig ResolveSurroundConfig(float localX, float localZ)
            {
                var heightResolution = config.Resolution;
                var heightX = Mathf.Clamp(
                    Mathf.RoundToInt(localX / config.terrainWidth * (heightResolution - 1)), 0, heightResolution - 1);
                var heightZ = Mathf.Clamp(
                    Mathf.RoundToInt(localZ / config.terrainLength * (heightResolution - 1)), 0, heightResolution - 1);
                var pixelIndex = heightZ * heightResolution + heightX;

                var bestBiome = 0;
                var maxWeight = 0f;
                for (var biome = 0; biome < biomeCount; biome++)
                {
                    var weight = biomeWeights[pixelIndex, 2 + biome];
                    if (maxWeight < weight)
                    {
                        maxWeight = weight;
                        bestBiome = biome;
                    }
                }

                return surroundConfigs[bestBiome];
            }

            #endregion
        }
    }
}
