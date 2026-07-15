using System;
using System.Collections.Generic;
using System.Text;
using MapGenerator.Pipeline;
using MapGenerator.Pipeline.Config;
using MessagePack;
using UnityEditor;
using UnityEngine;

namespace MapGenerator.Editor
{
    /// <summary>
    /// マップ生成結果のデータサイズを計測する。
    /// 生データサイズとMessagePack+LZ4圧縮サイズを比較するための診断ツール。
    /// </summary>
    public static class DataSizeMeasurement
    {
        // MessagePack用のシリアライズ可能なタイル構造体
        // 実際にネットワーク/ストレージで送信するフィールドのみ含む
        [MessagePackObject]
        public class TileData
        {
            [Key(0)] public float[] Heights;
            [Key(1)] public int HeightmapResolution;
            [Key(2)] public float[] SplatmapFlat; // [y,x,layer] → flat
            [Key(3)] public int SplatmapResX;
            [Key(4)] public int SplatmapResY;
            [Key(5)] public int SplatmapLayers;
            // Tree: x,y,z,widthScale,heightScale,rotation の6floatずつ
            [Key(6)] public float[] TreeData;
            [Key(7)] public int[] TreeProtoIndices;
            [Key(8)] public int[][] DetailMaps; // [protoIndex][y*w+x]
            [Key(9)] public int DetailResolution;
            [Key(10)] public float[] ObjectPositions; // x,y,z per object
            [Key(11)] public float[] ObjectRotations; // x,y,z,w per object
            [Key(12)] public float[] ObjectScales; // x,y,z per object
            [Key(13)] public int[] ObjectPrefabIds;
            [Key(14)] public float[] OrePositions;
            [Key(15)] public float[] OreRotations;
            [Key(16)] public float[] OreScales;
            [Key(17)] public int[] OrePrefabIds;
        }

        [MenuItem("Tools/MapGenerator/Measure Data Size")]
        public static void MeasureDataSize()
        {
            // InfiniteTerrainManager または MapGeneratorFacade から config を取得
            TerrainGenerationConfig config = null;

            var itm = UnityEngine.Object.FindFirstObjectByType<InfiniteTerrainManager>();
            if (itm != null) config = itm.baseConfig;

            if (config == null)
            {
                var facade = UnityEngine.Object.FindFirstObjectByType<MapGeneratorFacade>();
                if (facade != null) config = facade.config;
            }

            if (config == null)
            {
                Debug.LogError("[DataSizeMeasurement] TerrainGenerationConfig not found in scene");
                return;
            }

            var result = TerrainGenerator.Generate(config);

            int tilesX = config.gridSizeX;
            int tilesZ = config.gridSizeZ;
            int totalTiles = tilesX * tilesZ;
            int fullRes = result.Resolution;

            var sb = new StringBuilder();
            sb.AppendLine("========== マップデータサイズ計測 ==========");
            sb.AppendLine($"解像度: {fullRes}x{fullRes} (プリセット: {config.resolutionPreset})");
            sb.AppendLine($"グリッド: {tilesX}x{tilesZ} = {totalTiles} タイル");
            sb.AppendLine($"地形サイズ: {config.terrainWidth}x{config.terrainLength}m");
            sb.AppendLine();

            // タイルデータを先に全抽出（正確な計測のため）
            var allTiles = new List<TileData>();
            for (int tz = 0; tz < tilesZ; tz++)
                for (int tx = 0; tx < tilesX; tx++)
                    allTiles.Add(ExtractTileData(result, tx, tz, tilesX, tilesZ));

            // --- 全体の生データサイズ = シリアライズ対象フィールドのバイト数 ---
            long totalRawBytes = 0;
            foreach (var tile in allTiles)
                totalRawBytes += CalcRawBytes(tile);

            // 全体内訳（最初のタイルで代表表示）
            sb.AppendLine("[全体データ内訳]");

            // Heights
            int heightElems = result.Heights?.Length ?? 0;
            long heightRaw = heightElems * 4L;
            sb.AppendLine($"  Heights: {heightElems} floats = {FormatBytes(heightRaw)}");

            // Splatmap
            long splatRaw = 0;
            if (result.Splatmap != null)
            {
                int sy = result.Splatmap.GetLength(0);
                int sx = result.Splatmap.GetLength(1);
                int sl = result.Splatmap.GetLength(2);
                splatRaw = (long)sy * sx * sl * 4;
                sb.AppendLine($"  Splatmap: {sy}x{sx}x{sl} = {FormatBytes(splatRaw)}");
            }

            // Trees
            int treeCount = result.TreeInstances?.Length ?? 0;
            // 実際にシリアライズするフィールド: 6 floats + 1 int = 28 bytes/tree
            long treeRaw = treeCount * 28L;
            sb.AppendLine($"  Trees: {treeCount} instances x 28B = {FormatBytes(treeRaw)}");

            // DetailMaps
            long detailRaw = 0;
            int detailProtoCount = result.DetailMaps?.Count ?? 0;
            if (result.DetailMaps != null)
                foreach (var dm in result.DetailMaps)
                    detailRaw += dm.GetLength(0) * dm.GetLength(1) * 4L;
            sb.AppendLine($"  DetailMaps: {detailProtoCount} layers = {FormatBytes(detailRaw)}");

            // Objects
            int objCount = result.ObjectPlacements?.Count ?? 0;
            // 実際にシリアライズ: pos(3f) + rot(4f) + scale(3f) + prefabId(1i) = 44 bytes
            long objRaw = objCount * 44L;
            sb.AppendLine($"  Objects: {objCount} instances x 44B = {FormatBytes(objRaw)}");

            // Ores
            int oreCount = result.OrePlacements?.Count ?? 0;
            long oreRaw = oreCount * 44L;
            sb.AppendLine($"  Ores: {oreCount} instances x 44B = {FormatBytes(oreRaw)}");

            sb.AppendLine();
            sb.AppendLine($"★ 全体生データ合計: {FormatBytes(totalRawBytes)}");
            sb.AppendLine($"  (各タイル合算の実測値。上の内訳は全体フラットデータからの概算)");

            // --- 1タイルあたり（タイル0,0の実データで計測） ---
            sb.AppendLine();
            sb.AppendLine("---------- 1タイルあたり (タイル[0,0]の実データ) ----------");
            var tile0 = allTiles[0];
            long tile0Raw = CalcRawBytes(tile0);

            sb.AppendLine($"  Heights: {tile0.Heights?.Length ?? 0} floats = {FormatBytes((tile0.Heights?.Length ?? 0) * 4L)}");
            sb.AppendLine($"  Splatmap: {tile0.SplatmapFlat?.Length ?? 0} floats = {FormatBytes((tile0.SplatmapFlat?.Length ?? 0) * 4L)}");
            int tile0Trees = tile0.TreeProtoIndices?.Length ?? 0;
            sb.AppendLine($"  Trees: {tile0Trees} instances = {FormatBytes(tile0Trees * 28L)}");
            long tile0Detail = 0;
            if (tile0.DetailMaps != null)
                foreach (var dm in tile0.DetailMaps)
                    tile0Detail += dm.Length * 4L;
            sb.AppendLine($"  DetailMaps: {tile0.DetailMaps?.Length ?? 0} layers = {FormatBytes(tile0Detail)}");
            int tile0Objs = tile0.ObjectPrefabIds?.Length ?? 0;
            sb.AppendLine($"  Objects: {tile0Objs} instances = {FormatBytes(tile0Objs * 44L)}");
            int tile0Ores = tile0.OrePrefabIds?.Length ?? 0;
            sb.AppendLine($"  Ores: {tile0Ores} instances = {FormatBytes(tile0Ores * 44L)}");
            sb.AppendLine($"  ★ 1タイル生データ合計: {FormatBytes(tile0Raw)}");

            // --- MessagePack + LZ4 圧縮 ---
            sb.AppendLine();
            sb.AppendLine("---------- MessagePack + LZ4 圧縮 ----------");

            var optLz4 = MessagePackSerializerOptions.Standard
                .WithCompression(MessagePackCompression.Lz4BlockArray);

            byte[] tile0MsgpackRaw = MessagePackSerializer.Serialize(tile0);
            byte[] tile0MsgpackLz4 = MessagePackSerializer.Serialize(tile0, optLz4);
            sb.AppendLine($"1タイル MessagePack (無圧縮): {FormatBytes(tile0MsgpackRaw.Length)}");
            sb.AppendLine($"1タイル MessagePack + LZ4:    {FormatBytes(tile0MsgpackLz4.Length)}");

            byte[] allMsgpackRaw = MessagePackSerializer.Serialize(allTiles);
            byte[] allMsgpackLz4 = MessagePackSerializer.Serialize(allTiles, optLz4);
            sb.AppendLine($"全{totalTiles}タイル MessagePack (無圧縮): {FormatBytes(allMsgpackRaw.Length)}");
            sb.AppendLine($"全{totalTiles}タイル MessagePack + LZ4:    {FormatBytes(allMsgpackLz4.Length)}");

            // --- ラウンドトリップ検証 ---
            sb.AppendLine();
            sb.AppendLine("---------- ラウンドトリップ検証 ----------");
            var deserialized = MessagePackSerializer.Deserialize<TileData>(tile0MsgpackLz4, optLz4);
            bool heightsOk = deserialized.Heights != null && deserialized.Heights.Length == (tile0.Heights?.Length ?? 0);
            bool splatOk = deserialized.SplatmapFlat != null && deserialized.SplatmapFlat.Length == (tile0.SplatmapFlat?.Length ?? 0);
            bool treesOk = deserialized.TreeProtoIndices != null && deserialized.TreeProtoIndices.Length == tile0Trees;
            bool detailOk = deserialized.DetailMaps != null && deserialized.DetailMaps.Length == (tile0.DetailMaps?.Length ?? 0);
            sb.AppendLine($"  Heights round-trip: {(heightsOk ? "OK" : "FAIL")}");
            sb.AppendLine($"  Splatmap round-trip: {(splatOk ? "OK" : "FAIL")}");
            sb.AppendLine($"  Trees round-trip: {(treesOk ? "OK" : "FAIL")}");
            sb.AppendLine($"  DetailMaps round-trip: {(detailOk ? "OK" : "FAIL")}");

            // データ値の一致も確認
            if (heightsOk && tile0.Heights.Length > 0)
            {
                bool valuesMatch = true;
                for (int i = 0; i < tile0.Heights.Length; i++)
                    if (Math.Abs(tile0.Heights[i] - deserialized.Heights[i]) > 1e-7f) { valuesMatch = false; break; }
                sb.AppendLine($"  Heights values match: {(valuesMatch ? "OK" : "FAIL")}");
            }

            // --- 圧縮率 ---
            sb.AppendLine();
            sb.AppendLine("---------- 圧縮率サマリ ----------");
            sb.AppendLine($"1タイル: 生 {FormatBytes(tile0Raw)} → MsgPack {FormatBytes(tile0MsgpackRaw.Length)} → LZ4 {FormatBytes(tile0MsgpackLz4.Length)} ({(double)tile0MsgpackLz4.Length / tile0Raw * 100:F1}%)");
            sb.AppendLine($"全体:   生 {FormatBytes(totalRawBytes)} → MsgPack {FormatBytes(allMsgpackRaw.Length)} → LZ4 {FormatBytes(allMsgpackLz4.Length)} ({(double)allMsgpackLz4.Length / totalRawBytes * 100:F1}%)");

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// TileDataの全フィールドの生バイト数を計算する。
        /// MessagePackヘッダなしの純粋なデータ量。
        /// </summary>
        static long CalcRawBytes(TileData tile)
        {
            long bytes = 0;
            bytes += 4; // HeightmapResolution (int)
            bytes += (tile.Heights?.Length ?? 0) * 4L;
            bytes += (tile.SplatmapFlat?.Length ?? 0) * 4L;
            bytes += 4 + 4 + 4; // SplatmapResX, ResY, Layers
            bytes += (tile.TreeData?.Length ?? 0) * 4L;
            bytes += (tile.TreeProtoIndices?.Length ?? 0) * 4L;
            bytes += 4; // DetailResolution
            if (tile.DetailMaps != null)
                foreach (var dm in tile.DetailMaps)
                    bytes += dm.Length * 4L;
            bytes += (tile.ObjectPositions?.Length ?? 0) * 4L;
            bytes += (tile.ObjectRotations?.Length ?? 0) * 4L;
            bytes += (tile.ObjectScales?.Length ?? 0) * 4L;
            bytes += (tile.ObjectPrefabIds?.Length ?? 0) * 4L;
            bytes += (tile.OrePositions?.Length ?? 0) * 4L;
            bytes += (tile.OreRotations?.Length ?? 0) * 4L;
            bytes += (tile.OreScales?.Length ?? 0) * 4L;
            bytes += (tile.OrePrefabIds?.Length ?? 0) * 4L;
            return bytes;
        }

        static TileData ExtractTileData(TerrainGenerationResult result, int tileX, int tileZ, int tilesX,
            int tilesZ)
        {
            var tile = new TileData();
            int fullRes = result.Resolution;
            int tileResX = (fullRes - 1) / tilesX + 1;
            int tileResZ = (fullRes - 1) / tilesZ + 1;
            int tileRes = Mathf.Max(tileResX, tileResZ);

            // Heights
            if (result.Heights != null)
            {
                tile.HeightmapResolution = tileRes;
                tile.Heights = new float[tileRes * tileRes];
                int startX = tileX * (tileResX - 1);
                int startZ = tileZ * (tileResZ - 1);
                for (int y = 0; y < tileRes; y++)
                for (int x = 0; x < tileRes; x++)
                {
                    int srcX = Mathf.Min(startX + x, fullRes - 1);
                    int srcZ = Mathf.Min(startZ + y, fullRes - 1);
                    tile.Heights[y * tileRes + x] = result.Heights[srcZ * fullRes + srcX];
                }
            }

            // Splatmap
            if (result.Splatmap != null)
            {
                int fullAResX = result.Splatmap.GetLength(1);
                int fullAResZ = result.Splatmap.GetLength(0);
                int tileAResX = fullAResX / tilesX;
                int tileAResZ = fullAResZ / tilesZ;
                int tileARes = Mathf.Max(tileAResX, tileAResZ);
                int layers = result.Splatmap.GetLength(2);
                int aStartX = tileX * tileAResX;
                int aStartZ = tileZ * tileAResZ;

                tile.SplatmapResX = tileARes;
                tile.SplatmapResY = tileARes;
                tile.SplatmapLayers = layers;
                tile.SplatmapFlat = new float[tileARes * tileARes * layers];

                for (int y = 0; y < tileARes; y++)
                for (int x = 0; x < tileARes; x++)
                {
                    int srcX = Mathf.Min(aStartX + x, fullAResX - 1);
                    int srcZ = Mathf.Min(aStartZ + y, fullAResZ - 1);
                    int dstBase = (y * tileARes + x) * layers;
                    for (int l = 0; l < layers; l++)
                        tile.SplatmapFlat[dstBase + l] = result.Splatmap[srcZ, srcX, l];
                }
            }

            // Trees（全フィールドをシリアライズ対象に）
            if (result.TreeInstances != null)
            {
                float txMin = (float)tileX / tilesX;
                float txMax = (float)(tileX + 1) / tilesX;
                float tzMin = (float)tileZ / tilesZ;
                float tzMax = (float)(tileZ + 1) / tilesZ;

                var treeData = new List<float>();
                var indices = new List<int>();

                foreach (var tree in result.TreeInstances)
                {
                    if (tree.position.x >= txMin && tree.position.x < txMax &&
                        tree.position.z >= tzMin && tree.position.z < tzMax)
                    {
                        float lx = (tree.position.x - txMin) * tilesX;
                        float lz = (tree.position.z - tzMin) * tilesZ;
                        // x, y, z, widthScale, heightScale, rotation の6floatずつ
                        treeData.Add(lx);
                        treeData.Add(tree.position.y);
                        treeData.Add(lz);
                        treeData.Add(tree.widthScale);
                        treeData.Add(tree.heightScale);
                        treeData.Add(tree.rotation);
                        indices.Add(tree.prototypeIndex);
                    }
                }

                tile.TreeData = treeData.ToArray();
                tile.TreeProtoIndices = indices.ToArray();
            }

            // DetailMaps
            if (result.DetailMaps != null && result.DetailMaps.Count > 0)
            {
                int fullDRes = result.DetailMaps[0].GetLength(0);
                int tileDRes = fullDRes / tilesX;
                tile.DetailResolution = tileDRes;
                tile.DetailMaps = new int[result.DetailMaps.Count][];

                int dStartX = tileX * tileDRes;
                int dStartZ = tileZ * tileDRes;

                for (int i = 0; i < result.DetailMaps.Count; i++)
                {
                    tile.DetailMaps[i] = new int[tileDRes * tileDRes];
                    for (int y = 0; y < tileDRes; y++)
                    for (int x = 0; x < tileDRes; x++)
                        tile.DetailMaps[i][y * tileDRes + x] = result.DetailMaps[i][dStartZ + y, dStartX + x];
                }
            }

            // Objects（タイル範囲のワールド座標でフィルタ）
            float worldTileWidth = result.TerrainSize.x / tilesX;
            float worldTileLength = result.TerrainSize.z / tilesZ;
            float worldXMin = tileX * worldTileWidth;
            float worldXMax = (tileX + 1) * worldTileWidth;
            float worldZMin = tileZ * worldTileLength;
            float worldZMax = (tileZ + 1) * worldTileLength;

            ExtractPlacements(result.ObjectPlacements, worldXMin, worldXMax, worldZMin, worldZMax,
                out tile.ObjectPositions, out tile.ObjectRotations, out tile.ObjectScales, out tile.ObjectPrefabIds);
            ExtractPlacements(result.OrePlacements, worldXMin, worldXMax, worldZMin, worldZMax,
                out tile.OrePositions, out tile.OreRotations, out tile.OreScales, out tile.OrePrefabIds);

            return tile;
        }

        static void ExtractPlacements(List<ObjectPlacementResult> placements,
            float xMin, float xMax, float zMin, float zMax,
            out float[] positions, out float[] rotations, out float[] scales, out int[] prefabIds)
        {
            if (placements == null || placements.Count == 0)
            {
                positions = Array.Empty<float>();
                rotations = Array.Empty<float>();
                scales = Array.Empty<float>();
                prefabIds = Array.Empty<int>();
                return;
            }

            var pos = new List<float>();
            var rot = new List<float>();
            var scl = new List<float>();
            var ids = new List<int>();

            foreach (var p in placements)
            {
                if (p.Position.x >= xMin && p.Position.x < xMax &&
                    p.Position.z >= zMin && p.Position.z < zMax)
                {
                    pos.Add(p.Position.x);
                    pos.Add(p.Position.y);
                    pos.Add(p.Position.z);
                    rot.Add(p.Rotation.x);
                    rot.Add(p.Rotation.y);
                    rot.Add(p.Rotation.z);
                    rot.Add(p.Rotation.w);
                    scl.Add(p.Scale.x);
                    scl.Add(p.Scale.y);
                    scl.Add(p.Scale.z);
                    ids.Add(p.Prefab != null ? p.Prefab.GetInstanceID() : 0);
                }
            }

            positions = pos.ToArray();
            rotations = rot.ToArray();
            scales = scl.ToArray();
            prefabIds = ids.ToArray();
        }

        static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
        }
    }
}
