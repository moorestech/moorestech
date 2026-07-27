using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Game.MapGeneration.Export;
using Game.MapGeneration.Provisioning;
using Game.Paths;
using Newtonsoft.Json;

namespace Game.MapGeneration.Transfer
{
    // world.jsonとterrain実ファイルからTerrainTransferMetaを組み立てる読み取り専用の入口
    // Read-only entry point assembling TerrainTransferMeta from world.json and the real terrain files
    public static class TerrainTransferMetaReader
    {
        private const int WorldIdHexDigits = 16;

        public static TerrainTransferMeta Read(WorldDataDirectory worldDataDirectory)
        {
            // Rootがnullなのはワールドディレクトリを持たない構成という宣言であり、欠損の補完ではない
            // A null Root declares a configuration that owns no world directory; this is not filling in missing data
            if (worldDataDirectory.Root == null) return TerrainTransferMeta.CreateWithoutWorldDirectory();

            var worldMeta = JsonConvert.DeserializeObject<WorldMetaJson>(File.ReadAllText(worldDataDirectory.WorldMetaFilePath));

            // terrainを持つのはgeneratedのみ。未知のmapModeはフォールバックせず例外にする
            // Only generated worlds own terrain; an unknown map mode throws instead of falling back
            var chunkTotal = worldMeta.MapMode switch
            {
                WorldProvisioner.GeneratedMapMode => CalculateChunkTotal(),
                WorldProvisioner.TemplateMapMode => 0,
                _ => throw new InvalidOperationException($"Unknown map mode in world.json: '{worldMeta.MapMode}'")
            };

            return new TerrainTransferMeta(worldMeta.MapMode, CalculateWorldId(), worldMeta.TerrainResolution, worldMeta.TerrainTileCount, chunkTotal);

            #region Internal

            int CalculateChunkTotal()
            {
                // 論理ストリーム(タイル順のheight→biome)と同じ列挙で総バイトを出す。terrain/内の無関係ファイルは数えない
                // Sum bytes over the same enumeration the logical stream uses, so unrelated files in terrain/ never shift the count
                var totalBytes = TerrainTransferMeta.EnumerateTileCoordinates(worldMeta.TerrainTileCount).Sum(tile =>
                    new FileInfo(worldDataDirectory.TerrainHeightFilePath(tile.TileX, tile.TileZ)).Length +
                    new FileInfo(worldDataDirectory.TerrainBiomeFilePath(tile.TileX, tile.TileZ)).Length);
                return (int)((totalBytes + TerrainTransferMeta.ChunkByteSize - 1) / TerrainTransferMeta.ChunkByteSize);
            }

            string CalculateWorldId()
            {
                // seedとcreatedAtで識別する。同じseedを再生成したワールドも別IDになる
                // Identify by seed and createdAt, so regenerating the same seed still yields a distinct id
                using var sha256 = SHA256.Create();
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes($"{worldMeta.Seed}:{worldMeta.CreatedAt}"));
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant().Substring(0, WorldIdHexDigits);
            }

            #endregion
        }
    }
}
