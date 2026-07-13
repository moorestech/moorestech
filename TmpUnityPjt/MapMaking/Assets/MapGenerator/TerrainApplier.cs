using System.Collections.Generic;
using System.Threading.Tasks;
using MapGenerator.Pipeline;
using MapGenerator.Pipeline.Config;
using Unity.Collections;
using UnityEngine;

namespace MapGenerator
{
    /// <summary>
    /// パイプライン出力をUnity TerrainDataに書き込む。
    /// 単一テレインへの直接適用と、複数テレインへのタイル分割適用の両方に対応。
    /// NativeArray→マネージド配列の変換もここに集約する。
    /// </summary>
    public static class TerrainApplier
    {
        /// <summary>
        /// float[]をfloat[y,x]に変換する。heights[y * res + x] → heights2D[y, x]。
        /// ジョブ出力とマネージド配置処理の橋渡し。
        /// </summary>
        public static float[,] ConvertHeights(float[] flatHeights, int resolution)
        {
            var result = new float[resolution, resolution];
            for (int y = 0; y < resolution; y++)
                for (int x = 0; x < resolution; x++)
                    result[y, x] = flatHeights[y * resolution + x];
            return result;
        }

        /// <summary>
        /// ジョブ出力のフラットsplatWeightsをalphamap解像度にリサンプルしてfloat[y,x,layer]に変換する。
        /// heightmap解像度(res)とalphamap解像度(aRes)が異なる場合の座標変換も行う。
        /// </summary>
        public static float[,,] ConvertSplatWeights(
            NativeArray<float> splatWeights, int heightmapRes, int alphamapRes, int layerCount)
        {
            var result = new float[alphamapRes, alphamapRes, layerCount];
            int hRes = heightmapRes;
            int aRes = alphamapRes;
            int lc = layerCount;

            // 行単位で並列化。各ピクセルは独立（NativeArrayは読み取り専用）
            Parallel.For(0, aRes, y =>
            {
                for (int x = 0; x < aRes; x++)
                {
                    // alphamap座標→heightmap座標への変換
                    int hx = Mathf.Clamp(
                        Mathf.RoundToInt((float)x / (aRes - 1) * (hRes - 1)), 0, hRes - 1);
                    int hy = Mathf.Clamp(
                        Mathf.RoundToInt((float)y / (aRes - 1) * (hRes - 1)), 0, hRes - 1);
                    int srcIdx = hy * hRes + hx;

                    for (int l = 0; l < lc; l++)
                        result[y, x, l] = splatWeights[srcIdx * lc + l];
                }
            });

            return result;
        }

        // placementOffset: Object/Oreはノイズ座標(coord*ChunkWidth+G)で算出されるため-Gを渡しTerrain(coord*ChunkWidth)と同じシーン座標へ揃える加算値
        // 単一マップ生成ではworldOffsetをずらさないため default(=0)
        public static void Apply(TerrainData terrainData, TerrainGenerationResult result, Vector3 placementOffset = default)
        {
            int res = result.Resolution;

            // Heights=null の場合はハイトマップ適用をスキップ（既存データを保持）
            if (result.Heights != null)
            {
                terrainData.heightmapResolution = res;
                terrainData.size = result.TerrainSize;

                var heights2D = new float[res, res];
                for (int y = 0; y < res; y++)
                    for (int x = 0; x < res; x++)
                        heights2D[y, x] = result.Heights[y * res + x];

                terrainData.SetHeights(0, 0, heights2D);
            }

            if (result.TerrainLayers != null && result.Splatmap != null)
            {
                int sRes = result.Splatmap.GetLength(0);
                terrainData.alphamapResolution = sRes;
                terrainData.terrainLayers = result.TerrainLayers;
                terrainData.SetAlphamaps(0, 0, result.Splatmap);
            }

            if (result.TreePrototypes != null && result.TreeInstances != null)
            {
                terrainData.treePrototypes = result.TreePrototypes;
                terrainData.SetTreeInstances(result.TreeInstances, true);
            }

            // ディテール（草花）レイヤーの適用
            if (result.DetailPrototypes != null && result.DetailMaps != null && result.DetailPrototypes.Count > 0)
            {
                int detailRes = result.DetailMaps[0].GetLength(0);
                terrainData.SetDetailResolution(detailRes, 16);
                // CoverageModeではメッシュDetailが描画されない場合があるため、InstanceCountModeを使用
                terrainData.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
                terrainData.detailPrototypes = result.DetailPrototypes.ToArray();
                for (int i = 0; i < result.DetailMaps.Count; i++)
                    terrainData.SetDetailLayer(0, 0, i, result.DetailMaps[i]);
            }

            // オブジェクト（岩石・小物プレハブ）のインスタンス化
            if (result.ObjectPlacements != null && result.ObjectPlacements.Count > 0)
            {
                ApplyObjects(result.ObjectPlacements, placementOffset);
            }

            // 鉱脈（鉱石プレハブ）のインスタンス化
            if (result.OrePlacements != null && result.OrePlacements.Count > 0)
            {
                ApplyOres(result.OrePlacements, placementOffset);
            }
        }

        /// <summary>
        /// シーン上のTerrain配列からグリッド配置を自動検出し、フル生成結果をタイル分割して適用する。
        /// 境界ピクセルを共有することでタイル間のシームレス接続を保証する。
        /// </summary>
        public static void ApplyToGrid(Terrain[] terrains, TerrainGenerationResult result)
        {
            // 各TerrainのX/Z位置からグリッド構造を推定する
            var xSet = new SortedSet<float>();
            var zSet = new SortedSet<float>();
            foreach (var t in terrains)
            {
                xSet.Add(Mathf.Round(t.transform.position.x));
                zSet.Add(Mathf.Round(t.transform.position.z));
            }

            var xList = new List<float>(xSet);
            var zList = new List<float>(zSet);
            int tilesX = xList.Count;
            int tilesZ = zList.Count;

            // グリッドに各Terrainを配置し、タイルデータを適用
            var grid = new Terrain[tilesX, tilesZ];
            foreach (var terrain in terrains)
            {
                float px = Mathf.Round(terrain.transform.position.x);
                float pz = Mathf.Round(terrain.transform.position.z);
                int tx = xList.IndexOf(px);
                int tz = zList.IndexOf(pz);
                grid[tx, tz] = terrain;

                ApplyTile(terrain.terrainData, result, tx, tz, tilesX, tilesZ);
            }

            // Unity組み込みのLODシーム処理用に隣接関係を設定
            for (int tx = 0; tx < tilesX; tx++)
            {
                for (int tz = 0; tz < tilesZ; tz++)
                {
                    if (grid[tx, tz] == null) continue;
                    Terrain left = tx > 0 ? grid[tx - 1, tz] : null;
                    Terrain right = tx < tilesX - 1 ? grid[tx + 1, tz] : null;
                    Terrain top = tz < tilesZ - 1 ? grid[tx, tz + 1] : null;
                    Terrain bottom = tz > 0 ? grid[tx, tz - 1] : null;
                    grid[tx, tz].SetNeighbors(left, top, right, bottom);
                    grid[tx, tz].Flush();
                }
            }

            // オブジェクトはワールド座標で配置されるため、タイル分割不要で一括適用
            // 単一マップ生成はworldOffsetをずらさないためオフセット不要(=0)
            if (result.ObjectPlacements != null && result.ObjectPlacements.Count > 0)
            {
                ApplyObjects(result.ObjectPlacements, Vector3.zero);
            }

            // 鉱脈はワールド座標で配置されるため、タイル分割不要で一括適用
            if (result.OrePlacements != null && result.OrePlacements.Count > 0)
            {
                ApplyOres(result.OrePlacements, Vector3.zero);
            }
        }

        /// <summary>
        /// フル生成結果の一部をタイルとして抽出し、個別のTerrainDataに適用する。
        /// heightmapは境界ピクセルを隣接タイルと共有するため、シームが発生しない。
        /// </summary>
        static void ApplyTile(TerrainData terrainData, TerrainGenerationResult result,
            int tileX, int tileZ, int tilesX, int tilesZ)
        {
            int fullRes = result.Resolution;
            // X/Z軸それぞれのタイル解像度を計算（非正方形グリッド対応）
            int tileResX = (fullRes - 1) / tilesX + 1;
            int tileResZ = (fullRes - 1) / tilesZ + 1;
            // Unity Terrainは正方形のheightmapを要求するため、大きい方に合わせる
            int tileRes = Mathf.Max(tileResX, tileResZ);
            float tileWidth = result.TerrainSize.x / tilesX;
            float tileLength = result.TerrainSize.z / tilesZ;

            // Heights=null の場合はハイトマップ適用をスキップ（既存データを保持）
            if (result.Heights != null)
            {
                terrainData.heightmapResolution = tileRes;
                terrainData.size = new Vector3(tileWidth, result.TerrainSize.y, tileLength);

                int startX = tileX * (tileResX - 1);
                int startZ = tileZ * (tileResZ - 1);
                var heights2D = new float[tileRes, tileRes];
                for (int y = 0; y < tileRes; y++)
                    for (int x = 0; x < tileRes; x++)
                    {
                        int srcX = Mathf.Min(startX + x, fullRes - 1);
                        int srcZ = Mathf.Min(startZ + y, fullRes - 1);
                        heights2D[y, x] = result.Heights[srcZ * fullRes + srcX];
                    }

                terrainData.SetHeights(0, 0, heights2D);
            }

            // スプラットマップは重複なし。非正方形グリッドでもX/Z軸それぞれで分割
            if (result.TerrainLayers != null && result.Splatmap != null)
            {
                int fullAResZ = result.Splatmap.GetLength(0);
                int fullAResX = result.Splatmap.GetLength(1);
                int tileAResX = fullAResX / tilesX;
                int tileAResZ = fullAResZ / tilesZ;
                int tileARes = Mathf.Max(tileAResX, tileAResZ);
                int aStartX = tileX * tileAResX;
                int aStartZ = tileZ * tileAResZ;
                int layers = result.Splatmap.GetLength(2);

                terrainData.alphamapResolution = tileARes;
                terrainData.terrainLayers = result.TerrainLayers;

                var tileSplatmap = new float[tileARes, tileARes, layers];
                for (int y = 0; y < tileARes; y++)
                    for (int x = 0; x < tileARes; x++)
                    {
                        int srcX = Mathf.Min(aStartX + x, fullAResX - 1);
                        int srcZ = Mathf.Min(aStartZ + y, fullAResZ - 1);
                        for (int l = 0; l < layers; l++)
                            tileSplatmap[y, x, l] = result.Splatmap[srcZ, srcX, l];
                    }

                terrainData.SetAlphamaps(0, 0, tileSplatmap);
            }

            // 樹木はタイル範囲内のみフィルタし、正規化座標をローカルに変換
            if (result.TreePrototypes != null && result.TreeInstances != null)
            {
                terrainData.treePrototypes = result.TreePrototypes;

                float txMin = (float)tileX / tilesX;
                float txMax = (float)(tileX + 1) / tilesX;
                float tzMin = (float)tileZ / tilesZ;
                float tzMax = (float)(tileZ + 1) / tilesZ;

                var tileTrees = new List<TreeInstance>();
                foreach (var tree in result.TreeInstances)
                {
                    if (tree.position.x >= txMin && tree.position.x < txMax &&
                        tree.position.z >= tzMin && tree.position.z < tzMax)
                    {
                        var localTree = tree;
                        localTree.position = new Vector3(
                            (tree.position.x - txMin) * tilesX,
                            tree.position.y,
                            (tree.position.z - tzMin) * tilesZ);
                        tileTrees.Add(localTree);
                    }
                }

                terrainData.SetTreeInstances(tileTrees.ToArray(), true);
            }

            // ディテールマップはスプラットマップと同様に重複なしで分割
            if (result.DetailPrototypes != null && result.DetailMaps != null && result.DetailPrototypes.Count > 0)
            {
                int fullDRes = result.DetailMaps[0].GetLength(0);
                int tileDRes = fullDRes / tilesX;
                int dStartX = tileX * tileDRes;
                int dStartZ = tileZ * tileDRes;

                terrainData.SetDetailResolution(tileDRes, 16);
                terrainData.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
                terrainData.detailPrototypes = result.DetailPrototypes.ToArray();

                for (int i = 0; i < result.DetailMaps.Count; i++)
                {
                    var tileDetail = new int[tileDRes, tileDRes];
                    for (int y = 0; y < tileDRes; y++)
                        for (int x = 0; x < tileDRes; x++)
                            tileDetail[y, x] = result.DetailMaps[i][dStartZ + y, dStartX + x];

                    terrainData.SetDetailLayer(0, 0, i, tileDetail);
                }
            }
        }

        /// <summary>
        /// 配置リストからプレハブをインスタンス化し、親オブジェクトにまとめる。
        /// 再生成時は既存の親を破棄してからクリーンに再作成する。
        /// </summary>
        static void ApplyObjects(List<ObjectPlacementResult> placements, Vector3 placementOffset)
        {
            // 共通親を取得 or 作成（マルチチャンクで蓄積するため破棄しない）
            var parent = GameObject.Find("MapGenerator_Objects");
            if (parent == null)
            {
                parent = new GameObject("MapGenerator_Objects");
#if UNITY_EDITOR
                UnityEditor.Undo.RegisterCreatedObjectUndo(parent, "Generate Objects");
#endif
            }

            foreach (var p in placements)
            {
                if (p.Prefab == null) continue;
#if UNITY_EDITOR
                var instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(p.Prefab);
#else
                var instance = Object.Instantiate(p.Prefab);
#endif
                instance.transform.SetParent(parent.transform);

                // パディング境界でハイトマップ参照がずれる場合があるため、
                // 配置後にTerrain.SampleHeightで地面に再スナップする。
                // placementOffsetでノイズ座標→シーン座標へ写してからTerrainを検索する。
                var pos = p.Position + placementOffset;
                var terrain = FindTerrainAt(pos);
                if (terrain != null)
                {
                    float groundY = terrain.SampleHeight(pos) + terrain.transform.position.y;
                    // sinkをスケールに比例させる（メッシュ原点が中心のプレハブに対応）
                    pos.y = groundY - p.Sink * p.Scale.y;
                }
                instance.transform.position = pos;
                instance.transform.rotation = p.Rotation;
                instance.transform.localScale = p.Scale;
            }
        }

        /// <summary>
        /// 鉱脈配置リストからプレハブをインスタンス化し、専用の親オブジェクトにまとめる。
        /// ApplyObjectsと同じロジックだが、親を分離して管理しやすくする。
        /// </summary>
        static void ApplyOres(List<ObjectPlacementResult> placements, Vector3 placementOffset)
        {
            var parent = GameObject.Find("MapGenerator_Ores");
            if (parent == null)
            {
                parent = new GameObject("MapGenerator_Ores");
#if UNITY_EDITOR
                UnityEditor.Undo.RegisterCreatedObjectUndo(parent, "Generate Ores");
#endif
            }

            foreach (var p in placements)
            {
                if (p.Prefab == null) continue;
#if UNITY_EDITOR
                var instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(p.Prefab);
#else
                var instance = Object.Instantiate(p.Prefab);
#endif
                instance.transform.SetParent(parent.transform);

                // placementOffsetでノイズ座標→シーン座標へ写してからTerrainを検索する。
                var pos = p.Position + placementOffset;
                var terrain = FindTerrainAt(pos);
                if (terrain != null)
                {
                    float groundY = terrain.SampleHeight(pos) + terrain.transform.position.y;
                    pos.y = groundY;
                }
                // 鉱脈は常にグリッド（整数ワールド座標）へスナップする。
                // X/Zは生成器で整数化済みだが、Yは上のSampleHeightで再算出されるためここで整数化する。
                pos.x = Mathf.Round(pos.x);
                pos.y = Mathf.Round(pos.y);
                pos.z = Mathf.Round(pos.z);
                instance.transform.position = pos;
                instance.transform.rotation = p.Rotation;
                instance.transform.localScale = p.Scale;
            }
        }

        /// <summary>
        /// 指定ワールド座標を含むTerrainを検索する。見つからなければnull。
        /// </summary>
        static Terrain FindTerrainAt(Vector3 worldPos)
        {
            foreach (var t in Terrain.activeTerrains)
            {
                var tPos = t.transform.position;
                var tSize = t.terrainData.size;
                if (worldPos.x >= tPos.x && worldPos.x <= tPos.x + tSize.x &&
                    worldPos.z >= tPos.z && worldPos.z <= tPos.z + tSize.z)
                    return t;
            }
            return null;
        }
    }
}
