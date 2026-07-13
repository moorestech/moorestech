using MapGenerator.Pipeline;
using MapGenerator.Pipeline.Spawn;
using System.Collections.Generic;
using UnityEngine;

namespace MapGenerator
{
    /// <summary>
    /// エディタ上でテレインチャンクをグリッド生成・管理する。
    /// 各チャンクはワールドオフセット付きで独立にパイプライン実行される。
    /// </summary>
    public class InfiniteTerrainManager : MonoBehaviour
    {
        // SOアセット参照。GenerateChunk()で一時的にフィールドを変更するが、try/finallyで復元される
        public TerrainGenerationConfig baseConfig;

        [Header("表示設定")]
        [Tooltip("URP Terrain Litマテリアル（未設定時はデフォルトマテリアル）")]
        public Material terrainMaterial;
        [Tooltip("ディテール（草花）の描画距離（m）")]
        public float detailObjectDistance = 200f;
        // 格納密度 × detailObjectDensity = 実機描画密度。参照値 rendered=1.16/m² を狙う係数
        [Tooltip("Detail格納密度に掛ける係数。実機描画本数/m² = 格納密度 × この値")]
        [Range(0.05f, 1f)] public float detailObjectDensity = 0.3f;

        // Inspector上でONにすると、パラメータ変更時にデバウンス付きで全チャンク再生成する
        public bool autoGenerate = true;

        // 生成済みチャンクをグリッド座標で管理
        readonly Dictionary<Vector2Int, Terrain> _chunks = new Dictionary<Vector2Int, Terrain>();
        // スポーン探索で算出したグローバルオフセット G（ワールドm）。各チャンクのノイズサンプル座標(worldOffset)にのみ加算する。
        // GameObjectの物理位置には加算しない（グリッドは原点中心に描画し、サンプルするノイズ領域だけをスポーン地点へずらす）。
        Vector2 _activeSpawnOffset = Vector2.zero;
        float ChunkWidth => baseConfig.terrainWidth;
        float ChunkLength => baseConfig.terrainLength;

        void GenerateChunk(Vector2Int coord)
        {
            // baseConfigのオフセットを一時変更して生成（アセット参照を保持するためクローンしない）
            float prevOffX = baseConfig.worldOffsetX;
            float prevOffZ = baseConfig.worldOffsetZ;
            baseConfig.worldOffsetX = coord.x * ChunkWidth + _activeSpawnOffset.x;
            baseConfig.worldOffsetZ = coord.y * ChunkLength + _activeSpawnOffset.y;

            // blendRadiusの半分をパディングに使用（blur半径がblendRadius/4なので十分）
            baseConfig.chunkPadding = Mathf.Max(baseConfig.chunkPadding, baseConfig.biomeBlendRadius / 2);

            // パディング付き生成でチャンク境界シームを解消
            TerrainGenerationResult result;
            try { result = TerrainGenerator.GenerateWithPadding(baseConfig); }
            finally { baseConfig.worldOffsetX = prevOffX; baseConfig.worldOffsetZ = prevOffZ; }

            // Terrain GameObjectを作成
            var go = new GameObject($"Chunk_{coord.x}_{coord.y}");
            go.transform.SetParent(transform);
            // 物理位置はオフセットを加えず原点中心グリッドのまま。ノイズサンプルは worldOffset 側で G ずらしている。
            go.transform.position = new Vector3(
                coord.x * ChunkWidth, 0,
                coord.y * ChunkLength);

            var terrainData = new TerrainData();
            var terrain = go.AddComponent<Terrain>();
            var collider = go.AddComponent<TerrainCollider>();
            terrain.terrainData = terrainData;
            collider.terrainData = terrainData;
            // URP Terrain Litマテリアルを適用（未設定だとピンクになる）
            if (terrainMaterial != null)
                terrain.materialTemplate = terrainMaterial;
            // ディテールの描画距離・密度係数を設定
            terrain.detailObjectDistance = detailObjectDistance;
            terrain.detailObjectDensity = detailObjectDensity;
            // ツリーのビルボード化を遠距離に設定（三角形シルエットを維持）
            terrain.treeBillboardDistance = 300f;
            terrain.treeMaximumFullLODCount = 500;

            // Object/Oreはノイズ座標(coord*ChunkWidth+G)で算出されTerrainは原点グリッド(coord*ChunkWidth)に置くため-Gでシーン座標へ揃える
            // （これをしないとズレ=Gで配置物が遠方に飛ぶ）
            TerrainApplier.Apply(terrainData, result,
                new Vector3(-_activeSpawnOffset.x, 0f, -_activeSpawnOffset.y));

            // 隣接チャンクのTerrainとネイバー接続
            SetNeighbors(coord, terrain);

            _chunks[coord] = terrain;
        }

        void SetNeighbors(Vector2Int coord, Terrain terrain)
        {
            _chunks.TryGetValue(coord + Vector2Int.left, out var left);
            _chunks.TryGetValue(coord + Vector2Int.right, out var right);
            _chunks.TryGetValue(coord + Vector2Int.up, out var top);
            _chunks.TryGetValue(coord + Vector2Int.down, out var bottom);
            terrain.SetNeighbors(left, top, right, bottom);

            // 既存の隣接チャンクのネイバーも更新
            if (left != null) left.SetNeighbors(null, null, terrain, null);
            if (right != null) right.SetNeighbors(terrain, null, null, null);
            if (top != null) top.SetNeighbors(null, null, null, terrain);
            if (bottom != null) bottom.SetNeighbors(null, terrain, null, null);
        }

        /// <summary>
        /// gridSizeX × gridSizeZ のグリッドで全チャンクを再生成する。
        /// エディタのGenerate Allボタンおよび自動再生成で使用。
        /// </summary>
        public void RegenerateAllChunks()
        {
            ClearAllChunks();

            _activeSpawnOffset = Vector2.zero;
            if (baseConfig.useSpawnOffsetSearch)
            {
                var biomeTypes = TerrainGenerator.GetEnabledBiomeTypesPublic(baseConfig);
                var result = SpawnRegionFinder.Find(baseConfig, biomeTypes);
                Debug.Log($"[SpawnSearch] {(result.Success ? "成功" : "フォールバック")}\n{result.Diagnostics}");
                // 成功/フォールバックいずれもオフセットとspawnを常に同期させ、ore帯中心を実オフセットと整合させる。
                // フォールバック時は offset=0 / spawn=gridCenter となり、古いSが残留して位置がズレるのを防ぐ。
                _activeSpawnOffset = result.WorldOffset;
                baseConfig.spawnWorldPosition = result.SpawnWorldPosition;
            }

            // baseConfig.gridSizeに基づいて原点中心のグリッドを生成
            int halfX = baseConfig.gridSizeX / 2;
            int halfZ = baseConfig.gridSizeZ / 2;
            int gridSizeX = baseConfig.gridSizeX;
            int gridSizeZ = baseConfig.gridSizeZ;
            for (int cx = -halfX; cx < gridSizeX - halfX; cx++)
                for (int cz = -halfZ; cz < gridSizeZ - halfZ; cz++)
                    GenerateChunk(new Vector2Int(cx, cz));
        }

        /// <summary>
        /// 全チャンクを破棄して初期状態に戻す。子GameObjectも含めて確実にクリアする。
        /// </summary>
        public void ClearAllChunks()
        {
            _chunks.Clear();
            // _chunksに未登録の手動生成チャンクも含めて子オブジェクトを全削除
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
            // オブジェクト配置の親も削除（マルチチャンクで蓄積されるため）
            var objParent = GameObject.Find("MapGenerator_Objects");
            if (objParent != null)
                DestroyImmediate(objParent);
            var oreParent = GameObject.Find("MapGenerator_Ores");
            if (oreParent != null)
                DestroyImmediate(oreParent);
        }
    }
}
