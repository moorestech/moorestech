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
        // 切り出しもここが持つ。到達半径を導くguidマップと同じ場所に置かないと、呼び出し側が窓を狭めても誰も気付けない
        // The slice lives here too: away from the guid map the reach is derived from, a caller narrowing the window would go unnoticed
        public static float[,] Apply(
            float[,] preHeights, TerrainGenerationConfig tileConfig,
            Vector3 tileWorldPosition, IReadOnlyList<MapObjectLayoutMessagePack> mapObjects)
        {
            // guidマップは有効バイオームのtreePlacementだけから建つ。岩や鉱脈のguidは載らず Apply 側の引きで落ちる
            // The guid map is built only from the enabled biomes' treePlacement, so rock and vein guids miss Apply's own lookup
            var helper = new BiomePlacementHelper(tileConfig);
            var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(tileConfig);
            var guidModMap = TreeHeightModifier.BuildGuidModMap(helper, biomeTypes);

            // 摂動はタイル境界の外の木からも届く。等倍で切り出すと片側の辺だけが持ち上がり、境界に縦の崖が立つ
            // The perturbation reaches in from trees past the tile boundary; a plain slice lifts one edge only and stands a cliff along the seam
            var halo = TreeHeightModifier.MaxReach(tileConfig, guidModMap);

            // 到達半径0なら摂動は1画素も動かさない。全画素の往復コピーごと省いて摂動前をそのまま表示に使う
            // A zero reach moves no pixel, so the whole round trip is skipped and the pre-tree heights are displayed as they are
            if (halo <= 0f) return preHeights;

            var resolution = tileConfig.Resolution;
            var flatHeights = new float[resolution * resolution];
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
                flatHeights[z * resolution + x] = preHeights[z, x];

            var tileLocalObjects = TileMapObjectSlicer.SliceWithHalo(
                mapObjects, tileWorldPosition, tileConfig.terrainWidth, tileConfig.terrainLength, halo);

            TreeHeightModifier.Apply(
                flatHeights, tileConfig, ToPlacementEntries(tileLocalObjects), guidModMap);

            var postHeights = new float[resolution, resolution];
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
                postHeights[z, x] = flatHeights[z * resolution + x];

            return postHeights;

            #region Internal

            // WorldPositionはタイルローカル。halo内のタイル外の木は負値やtileWidth超で入り、TreeHeightModifierが格子外の画素を捨てる
            // WorldPosition is tile-local; trees inside the halo but outside the tile arrive negative or past tileWidth and TreeHeightModifier drops the off-lattice pixels
            List<PlacementEntry> ToPlacementEntries(IReadOnlyList<TileLocalMapObject> haloObjects)
            {
                var entries = new List<PlacementEntry>(haloObjects.Count);
                foreach (var mapObject in haloObjects)
                    entries.Add(new PlacementEntry
                    {
                        MapObjectGuid = mapObject.Guid,
                        WorldPosition = mapObject.LocalPosition,
                    });

                return entries;
            }

            #endregion
        }
    }
}
