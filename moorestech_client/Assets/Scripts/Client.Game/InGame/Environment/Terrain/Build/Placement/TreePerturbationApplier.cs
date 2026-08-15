using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Stages;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Build.Placement
{
    /// <summary>
    ///     転送された摂動前の高さへ、配置済み樹木ぶんのガウシアン摂動を順に足して表示用の高さを作る。
    ///     サーバーは摂動前を正本として保存する（R12）ので、摂動が足されるのはクライアントのこの1箇所だけ
    ///     Adds the placed trees' Gaussian perturbation onto the transferred pre-tree heights to form the display heights;
    ///     the server persists the pre-tree heights as the source of truth (R12), so this is the sole place it is added
    /// </summary>
    public static class TreePerturbationApplier
    {
        // 入力は書き換えない。摂動前の高さはsplatとdetail密度がこの後も読むため、写してから足す
        // The input is never mutated: splat and detail density read the pre-tree heights afterwards, so the sum lands in a copy
        public static float[,] Apply(
            float[,] preHeights, TerrainGenerationConfig tileConfig,
            IReadOnlyList<MapObjectLayoutMessagePack> tileLocalObjects)
        {
            var resolution = tileConfig.Resolution;
            var flatHeights = new float[resolution * resolution];
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
                flatHeights[z * resolution + x] = preHeights[z, x];

            // guidマップは有効バイオームのtreePlacementだけから建つ。岩や鉱脈のguidは載らず Apply 側の引きで落ちる
            // The guid map is built only from the enabled biomes' treePlacement, so rock and vein guids miss Apply's own lookup
            var helper = new BiomePlacementHelper(tileConfig);
            var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(tileConfig);
            var guidModMap = TreeHeightModifier.BuildGuidModMap(helper, biomeTypes);
            TreeHeightModifier.Apply(
                flatHeights, resolution, tileConfig, ToPlacementEntries(tileLocalObjects), guidModMap);

            var postHeights = new float[resolution, resolution];
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
                postHeights[z, x] = flatHeights[z * resolution + x];

            return postHeights;
        }

        // WorldPositionはタイルローカル。TreeHeightModifierがタイル寸法で割って格子へ写すため、シーン絶対座標では格子外を指す
        // WorldPosition is tile-local: TreeHeightModifier divides it by the tile size, so a scene-absolute value lands off the lattice
        private static List<PlacementEntry> ToPlacementEntries(IReadOnlyList<MapObjectLayoutMessagePack> tileLocalObjects)
        {
            var entries = new List<PlacementEntry>(tileLocalObjects.Count);
            foreach (var mapObject in tileLocalObjects)
                entries.Add(new PlacementEntry
                {
                    MapObjectGuid = mapObject.MapObjectGuid,
                    WorldPosition = new Vector3(mapObject.X, mapObject.Y, mapObject.Z),
                });

            return entries;
        }
    }
}
