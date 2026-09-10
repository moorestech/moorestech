using System;
using System.Collections.Generic;
using System.IO;
using Game.Map.Interface.Json;
using Game.MapGeneration.Export;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Newtonsoft.Json;

namespace Tests.Module
{
    /// <summary>
    ///     実生成を通さずタイル数と内容を制御した合成ワールドを作る。ファイルごとに異なる値で埋めて順序違反を検出可能にする。
    ///     unit層とpacket層の双方が同じ合成ワールドでチャンク往復を検証できるようTests.Moduleに置く。
    ///     Builds a synthetic world with a controlled tile count and content; distinct fill values expose any ordering slip.
    ///     It lives in Tests.Module so both the unit layer and the packet layer verify the chunk round trip on the same world.
    /// </summary>
    public static class SyntheticMultiTileWorldFactory
    {
        // 4タイル×各100KBで論理ストリーム400KB。チャンク境界(256KB)がファイル途中に落ちN=2になる
        // Four tiles of 100KB give a 400KB logical stream, so the 256KB boundary falls mid-file and N becomes 2
        public const int MultiChunkFileByteSize = 100 * 1024;
        public const int TileCount = 4;

        // 期待する並び順をテスト側の素材として直書きする。実装の列挙メソッドを使うと並び順の検証が循環する
        // Spell the expected order out here as test material; reusing the production enumerator would make the check circular
        public static string[] ExpectedStreamFilePaths(WorldDataDirectory worldDataDirectory)
        {
            return new[]
            {
                worldDataDirectory.TerrainHeightFilePath(0, 0),
                worldDataDirectory.TerrainHeightFilePath(1, 0),
                worldDataDirectory.TerrainHeightFilePath(0, 1),
                worldDataDirectory.TerrainHeightFilePath(1, 1),
            };
        }

        public static WorldDataDirectory Create(TerrainTransferTestScope testScope, int fileByteSize)
        {
            var worldDataDirectory = testScope.CreateEmptyWorldDataDirectory();
            Directory.CreateDirectory(worldDataDirectory.TerrainDirectory);

            WriteTerrainFiles();
            WriteMapJson();
            WriteWorldMetaJson();
            return worldDataDirectory;

            #region Internal

            void WriteTerrainFiles()
            {
                var streamFilePaths = ExpectedStreamFilePaths(worldDataDirectory);
                for (var fileIndex = 0; fileIndex < streamFilePaths.Length; fileIndex++)
                {
                    var fileBytes = new byte[fileByteSize];
                    for (var byteIndex = 0; byteIndex < fileBytes.Length; byteIndex++) fileBytes[byteIndex] = (byte)(fileIndex + 1);
                    File.WriteAllBytes(streamFilePaths[fileIndex], fileBytes);
                }
            }

            // packet層はDI構築時にmap.jsonを読むので、配置0件のmap.jsonを合成ワールドにも置く
            // The packet layer reads map.json while building the DI container, so the synthetic world carries an empty one
            void WriteMapJson()
            {
                var mapInfoJson = new MapInfoJson
                {
                    DefaultSpawnPointJson = new SpawnPointJson { X = 0f, Y = 0f, Z = 0f },
                    MapObjects = new List<MapObjectInfoJson>(),
                    MapVeins = new List<MapVeinInfoJson>(),
                };
                File.WriteAllText(worldDataDirectory.MapJsonFilePath, JsonConvert.SerializeObject(mapInfoJson, Formatting.Indented));
            }

            void WriteWorldMetaJson()
            {
                var worldMeta = new WorldMetaJson
                {
                    Seed = 1,
                    GeneratorVersion = WorldGeneratorVersion.Current,
                    Algorithm = "test",
                    MapMode = WorldMapMode.Generated,
                    CreatedAt = DateTime.UtcNow.ToString("O"),
                    TerrainResolution = 256,
                    TerrainTileCount = TileCount,

                    // generatedのworld.jsonは原点を必ず持つ契約。合成ワールドも原点0の実値として明示する
                    // A generated world.json always carries origins by contract, so the synthetic world states them explicitly as a real 0
                    TerrainNoiseOriginX = 0f,
                    TerrainNoiseOriginZ = 0f,
                    TerrainSceneOriginX = 0f,
                    TerrainSceneOriginZ = 0f,

                    // 指紋は必須契約だがチャンク読み出しの対象外
                    // The fingerprint is a required contract but out of scope for chunk reading
                    GenerationMasterFingerprint = "synthetic-fingerprint",

                    // 台帳の指紋も同じく必須契約。チャンク読み出しは見た目キャッシュを引かないので値は問わない
                    // The ledger digest is a required contract too; chunk reading never touches the visual cache, so its value is immaterial
                    PlacementLedgerDigest = "synthetic-ledger-digest",
                };
                File.WriteAllText(worldDataDirectory.WorldMetaFilePath, JsonConvert.SerializeObject(worldMeta, Formatting.Indented));
            }

            #endregion
        }
    }
}
