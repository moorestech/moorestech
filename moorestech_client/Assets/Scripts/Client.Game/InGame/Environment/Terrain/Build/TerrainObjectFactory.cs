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
        public static UnityEngine.Terrain Create(
            Transform parent, string terrainObjectName, Vector3 worldPosition, TerrainData terrainData, Material terrainMaterial,
            float detailObjectDistance, float detailObjectDensity)
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

            terrain.detailObjectDistance = detailObjectDistance;
            terrain.detailObjectDensity = detailObjectDensity;
            return terrain;
        }
    }
}
