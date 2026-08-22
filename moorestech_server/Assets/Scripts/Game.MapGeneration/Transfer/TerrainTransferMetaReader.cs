using System;
using System.IO;
using System.Linq;
using Game.MapGeneration.Export;
using Game.MapGeneration.Identity;
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
        public static TerrainTransferMeta Read(WorldDataDirectory worldDataDirectory)
        {
            // Rootがnullなのはワールドディレクトリを持たない構成という宣言であり、欠損の補完ではない
            // A null Root declares a configuration that owns no world directory; this is not filling in missing data
            if (worldDataDirectory.Root == null) return TerrainTransferMeta.CreateWithoutWorldDirectory();

            var worldMeta = JsonConvert.DeserializeObject<WorldMetaJson>(File.ReadAllText(worldDataDirectory.WorldMetaFilePath));

            // terrainと原点を持つのはgeneratedのみ。未知のmapModeはフォールバックせず例外にする
            // Only generated worlds own terrain and origins; an unknown map mode throws instead of falling back
            //
            // generatedはgeneratorVersion不一致を明示拒否する。旧バージョンの転送ファイル構成は新クライアントが読めない
            // Generated worlds explicitly reject a generatorVersion mismatch; an older transfer layout is unreadable by a new client
            return worldMeta.MapMode switch
            {
                WorldProvisioner.GeneratedMapMode when worldMeta.GeneratorVersion != WorldProvisioner.GeneratorVersion =>
                    throw new InvalidOperationException(
                        $"Generated world.json '{worldDataDirectory.WorldMetaFilePath}' was written by generator '{worldMeta.GeneratorVersion}', " +
                        $"but this build is '{WorldProvisioner.GeneratorVersion}'. The transferred terrain file layout changed " +
                        "(biome_x_z.bin output/transfer removed, clusters no longer leave the generation system). Delete the world directory and generate the world again."),
                WorldProvisioner.GeneratedMapMode => TerrainTransferMeta.CreateGenerated(
                    WorldIdentity.Calculate(worldMeta.Seed, worldMeta.CreatedAt), worldMeta.TerrainResolution, worldMeta.TerrainTileCount,
                    CalculateChunkTotal(), worldMeta.Seed, ReadGeneratedOrigins(), ReadGenerationMasterFingerprint()),
                WorldProvisioner.TemplateMapMode => TerrainTransferMeta.CreateTemplate(WorldIdentity.Calculate(worldMeta.Seed, worldMeta.CreatedAt), worldMeta.Seed),
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

            // 指紋はマスタからは復元できず、原点と同じく生成時にしか決まらない。旧world.jsonはキーごと欠けるので作り直しを促す
            // The fingerprint cannot be recovered from the master and, like the origins, exists only at generation; older world.json files lack the key entirely, so demand a regeneration
            string ReadGenerationMasterFingerprint()
            {
                if (worldMeta.GenerationMasterFingerprint == null)
                    throw new InvalidOperationException(
                        $"Generated world.json '{worldDataDirectory.WorldMetaFilePath}' has no generationMasterFingerprint key. " +
                        "It predates the generation master fingerprint; delete the world directory and generate the world again.");
                return worldMeta.GenerationMasterFingerprint;
            }

            int CalculateChunkTotal()
            {
                // 論理ストリームそのものの列挙で総バイトを出す。terrain/内の無関係ファイルは数えない
                // Sum bytes over the logical stream enumeration itself, so unrelated files in terrain/ never shift the count
                var totalBytes = TerrainTransferMeta.EnumerateStreamFilePaths(worldDataDirectory, worldMeta.TerrainTileCount)
                    .Sum(filePath => new FileInfo(filePath).Length);
                return (int)((totalBytes + TerrainTransferMeta.ChunkByteSize - 1) / TerrainTransferMeta.ChunkByteSize);
            }

            #endregion
        }
    }
}
