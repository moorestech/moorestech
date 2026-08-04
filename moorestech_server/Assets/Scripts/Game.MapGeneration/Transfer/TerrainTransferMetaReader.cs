using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Game.MapGeneration.Export;
using Game.MapGeneration.Provisioning;
using Game.Paths;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.MapGeneration.Transfer
{
    // ワールド実体から地形メタを構築する
    // Build terrain metadata from world files
    public static class TerrainTransferMetaReader
    {
        private const int WorldIdHexDigits = 16;

        public static TerrainTransferMeta Read(WorldDataDirectory worldDataDirectory)
        {
            // Rootがnullなのはワールドディレクトリを持たない構成という宣言であり、欠損の補完ではない
            // A null Root declares a configuration that owns no world directory; this is not filling in missing data
            if (worldDataDirectory.Root == null) return TerrainTransferMeta.CreateWithoutWorldDirectory();

            var worldMeta = JsonConvert.DeserializeObject<WorldMetaJson>(File.ReadAllText(worldDataDirectory.WorldMetaFilePath));

            // terrainと原点を持つのはgeneratedのみ。未知のmapModeはフォールバックせず例外にする
            // Only generated worlds own terrain and origins; an unknown map mode throws instead of falling back
            return worldMeta.MapMode switch
            {
                WorldProvisioner.GeneratedMapMode => TerrainTransferMeta.CreateGenerated(
                    CalculateWorldId(), worldMeta.TerrainResolution, worldMeta.TerrainTileCount,
                    CalculateChunkTotal(), worldMeta.Seed, ReadGeneratedOrigins()),
                WorldProvisioner.TemplateMapMode => TerrainTransferMeta.CreateTemplate(CalculateWorldId(), worldMeta.Seed),
                _ => throw new InvalidOperationException($"Unknown map mode in world.json: '{worldMeta.MapMode}'")
            };

            #region Internal

            // 原点は生成時にしか決まらず0でも補えない。旧バージョンのworld.jsonはキーごと欠けるので作り直しを促す
            // The origins exist only at generation and cannot be filled with 0; older world.json files lack the keys entirely, so demand a regeneration
            TerrainOrigins ReadGeneratedOrigins()
            {
                if (worldMeta.TerrainNoiseOriginX == null || worldMeta.TerrainNoiseOriginZ == null ||
                    worldMeta.TerrainSceneOriginX == null || worldMeta.TerrainSceneOriginZ == null)
                    throw new InvalidOperationException(
                        $"Generated world.json '{worldDataDirectory.WorldMetaFilePath}' has no terrain origin keys " +
                        "(terrainNoiseOriginX/Z, terrainSceneOriginX/Z). It predates the origin transfer; delete the world directory and generate the world again.");

                return new TerrainOrigins(
                    noiseOrigin: new Vector2(worldMeta.TerrainNoiseOriginX.Value, worldMeta.TerrainNoiseOriginZ.Value),
                    sceneOrigin: new Vector2(worldMeta.TerrainSceneOriginX.Value, worldMeta.TerrainSceneOriginZ.Value));
            }

            int CalculateChunkTotal()
            {
                // 論理ストリームそのものの列挙で総バイトを出す。terrain/内の無関係ファイルは数えない
                // Sum bytes over the logical stream enumeration itself, so unrelated files in terrain/ never shift the count
                var totalBytes = TerrainTransferMeta.EnumerateStreamFilePaths(worldDataDirectory, worldMeta.TerrainTileCount)
                    .Sum(filePath => new FileInfo(filePath).Length);
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
