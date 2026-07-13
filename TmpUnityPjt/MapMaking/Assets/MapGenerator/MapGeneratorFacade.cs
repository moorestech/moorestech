using MapGenerator.Pipeline;
using UnityEngine;

namespace MapGenerator
{
    /// <summary>
    /// マップ生成のエントリポイント。GameObjectにアタッチして使用する。
    /// 自身の子階層のTerrainのみ収集し、グリッド配置を推定してタイル分割適用する。
    /// </summary>
    public class MapGeneratorFacade : MonoBehaviour
    {
        public TerrainGenerationConfig config;

        // Inspector上でONにすると、パラメータ変更時にデバウンス付きで自動生成する
        public bool autoGenerate = true;

        /// <summary>
        /// 自身の子階層にあるTerrainのみ収集する。他系統のTerrainを巻き込まない
        /// </summary>
        public Terrain[] CollectTerrains()
        {
            return GetComponentsInChildren<Terrain>();
        }

        public void Generate()
        {
            var terrains = CollectTerrains();
            if (terrains.Length == 0)
            {
                Debug.LogWarning("[MapGenerator] No Terrain found in scene.");
                return;
            }

            // パイプライン実行 → フルサイズの島を1回生成し、タイル分割して各Terrainに適用
            var result = TerrainGenerator.Generate(config);
            TerrainApplier.ApplyToGrid(terrains, result);
            Debug.Log($"[MapGenerator] Generated terrain across {terrains.Length} tiles.");
        }
    }
}
