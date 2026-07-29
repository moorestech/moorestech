using Client.Common;
using Client.Game.InGame.BlockSystem;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Build
{
    /// <summary>
    ///     TerrainDataを載せたTerrain GameObjectを1枚組み立てる。MapMaking InfiniteTerrainManager.GenerateChunk の移植
    ///     Assembles one Terrain GameObject around a TerrainData; ported from MapMaking's InfiniteTerrainManager.GenerateChunk
    /// </summary>
    public static class TerrainObjectFactory
    {
        // Detailの描画距離(m)。既定の80mだと中距離で草が消えるため移植元と同じ200mにする
        // Detail render distance in metres; the 80m default cuts grass off mid-range, so match the source's 200m
        private const float DetailObjectDistance = 200f;

        // 格納密度に掛かる係数。実機描画本数/m² = 格納密度 × この値で、0.3は参照値rendered=1.16/m²を狙った実測値
        // Multiplier on the stored density: rendered instances/m² = stored density x this. 0.3 targets the reference rendered=1.16/m²
        private const float DetailObjectDensity = 0.3f;

        public static UnityEngine.Terrain Create(
            Transform parent, string terrainObjectName, Vector3 worldPosition, TerrainData terrainData, Material terrainMaterial)
        {
            // 設置プレビュー・露頭のレイキャストはGroundレイヤーとGroundGameObjectの両方で地面を判定する
            // Placement preview and outcrop raycasts identify ground by both the Ground layer and GroundGameObject
            var terrainObject = new GameObject(terrainObjectName) { layer = LayerConst.GroundLayer };
            terrainObject.AddComponent<GroundGameObject>();

            terrainObject.transform.SetParent(parent);
            terrainObject.transform.position = worldPosition;

            var terrain = terrainObject.AddComponent<UnityEngine.Terrain>();
            var terrainCollider = terrainObject.AddComponent<TerrainCollider>();
            terrain.terrainData = terrainData;
            terrainCollider.terrainData = terrainData;

            // 未設定だと地形がピンクになる。URPのdefaultTerrainMaterialはエディタ専用(ビルドでnull)なので使えず、呼び出し側がアドレスから解決したものを受け取る
            // Leaving this unset renders the terrain pink; URP's defaultTerrainMaterial is editor-only (null in builds), so the caller supplies one resolved from an address
            terrain.materialTemplate = terrainMaterial;

            terrain.detailObjectDistance = DetailObjectDistance;
            terrain.detailObjectDensity = DetailObjectDensity;
            return terrain;
        }
    }
}
