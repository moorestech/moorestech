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
        public static UnityEngine.Terrain Create(Transform parent, string terrainObjectName, Vector3 worldPosition, TerrainData terrainData)
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

            // materialTemplateは未設定のままにする。URPが差すデフォルトはEnvironment.prefabが指していたマテリアルと同一
            // materialTemplate is left unset: URP's default terrain material is the very one Environment.prefab referenced
            return terrain;
        }
    }
}
