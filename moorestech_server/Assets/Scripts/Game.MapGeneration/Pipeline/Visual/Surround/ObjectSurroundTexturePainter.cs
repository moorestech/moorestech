using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Pipeline.Visual.Splat;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Visual.Surround
{
    /// <summary>
    ///     岩をクラスタ単位にまとめ、その周りのalphamapを裸地レイヤーへ寄せる。移植元 TerrainGenerator.cs:1513-1571
    ///     と ResolveSurroundConfig(:1714-1743) の移植で、クラスタ内外の描画本体は2つの painter が持つ。
    ///     受け取るのはシーン絶対座標の全MapObjectで、到達距離ぶんの切り出しと種別分割はこのクラスの中で行う
    ///     Groups rocks by cluster and pulls the alphamap around them onto a bare-ground layer; ported from the source's
    ///     TerrainGenerator.cs:1513-1571 plus ResolveSurroundConfig (:1714-1743), with the two painters owning the drawing.
    ///     It takes every scene-absolute MapObject and does the reach-sized slice and the kind split itself
    /// </summary>
    public static class ObjectSurroundTexturePainter
    {
        // 隣タイルの岩からも裸地は伸びる。切り出しhaloがこの距離を下回るとタイル境界で裸地が直線に切れる
        // Bare ground reaches in from neighbouring tiles too; a slice halo below this distance breaks it in a straight line at the seam
        private static float MaxReach(
            SurroundTextureConfig[] surroundConfigs, IReadOnlyList<LedgerPlacement> placements)
        {
            var maxHorizontalScale = 0f;
            foreach (var mapObject in placements)
                maxHorizontalScale = Mathf.Max(maxHorizontalScale, (mapObject.Scale.x + mapObject.Scale.z) * 0.5f);

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

        // 到達距離を知っているのはここだけなので、切り出しと種別分割もここが持つ。呼び出し側に選ばせると木の距離を渡せてしまう
        // Only this class knows the reach, so it owns the slice and the kind split too; letting the caller choose lets a tree's reach slip in
        // 裸地を塗るのはrockBareGroundの岩だけ。rockNoBareGroundの瓦礫・メサは距離場にだけ乗り、ここでは触らない
        // Only rockBareGround rocks paint bare ground; rockNoBareGround rubble and mesas feed the distance field alone and are left untouched here
        public static void Apply(
            float[,,] alphamap, TerrainGenerationConfig config, SplatLayerTable layerTable,
            SurroundTextureConfig[] surroundConfigs, float[,] biomeWeights, int biomeCount,
            float[,] heights, IReadOnlyList<LedgerPlacement> placements, Vector3 tileWorldPosition)
        {
            TilePlacementSlicer.SliceKindsWithHalo(
                placements, tileWorldPosition, config.terrainWidth, config.terrainLength,
                MaxReach(surroundConfigs, placements), out _, out _, out var bareGroundStoneObjects);

            var clusterGroups = new Dictionary<int, List<TileLocalPlacement>>();
            var nonClusterObjects = new List<TileLocalPlacement>();
            GroupByCluster();

            // クラスタは重心のバイオームで設定を1つに決める。メンバーごとに引くとクラスタが境界を跨いだとき裸地が割れる
            // A cluster resolves one config at its centroid; resolving per member would split the bare ground where a cluster crosses a boundary
            foreach (var clusterGroup in clusterGroups)
            {
                var members = clusterGroup.Value;
                var clusterCenter = members[0].LocalCluster.Value.Center;
                var surroundConfig = ResolveSurroundConfig(clusterCenter.x, clusterCenter.y);
                if (!surroundConfig.enabled) continue;

                SurroundClusterPainter.Paint(
                    alphamap, config, surroundConfig, LayerIndexOf(surroundConfig), members, heights, tileWorldPosition);
            }

            foreach (var stoneObject in nonClusterObjects)
            {
                var surroundConfig = ResolveSurroundConfig(
                    stoneObject.LocalPosition.x, stoneObject.LocalPosition.z);
                if (!surroundConfig.enabled) continue;

                SurroundSingleRockPainter.Paint(
                    alphamap, config, surroundConfig, LayerIndexOf(surroundConfig), stoneObject, tileWorldPosition);
            }

            #region Internal

            void GroupByCluster()
            {
                foreach (var stoneObject in bareGroundStoneObjects)
                {
                    if (!stoneObject.LocalCluster.HasValue)
                    {
                        nonClusterObjects.Add(stoneObject);
                        continue;
                    }

                    var clusterId = stoneObject.LocalCluster.Value.Id;
                    if (!clusterGroups.TryGetValue(clusterId, out var members))
                    {
                        members = new List<TileLocalPlacement>();
                        clusterGroups[clusterId] = members;
                    }

                    members.Add(stoneObject);
                }
            }

            // 裸地レイヤーはマスタが必ずアドレスを持つ。SplatLayerTableが登録済みなので索引は必ず引ける
            // The bare-ground layer always carries an address in the master data and SplatLayerTable has registered it, so the lookup always resolves
            int LayerIndexOf(SurroundTextureConfig surroundConfig)
            {
                return layerTable.LayerIndexByAddress[surroundConfig.surroundLayerAddressablePath];
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
