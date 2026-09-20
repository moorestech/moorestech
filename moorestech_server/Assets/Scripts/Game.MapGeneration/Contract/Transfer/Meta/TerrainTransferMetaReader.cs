using System;
using System.IO;
using System.Linq;
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
            if (WorldMapMode.IsGenerated(worldMeta.MapMode)) return ReadGenerated();
            if (worldMeta.MapMode == WorldMapMode.Template)
                return new TemplateTerrainTransferMeta(WorldIdentity.CalculateTemplate(worldMeta.Seed, worldMeta.CreatedAt), worldMeta.Seed);
            throw new InvalidOperationException($"Unknown map mode in world.json: '{worldMeta.MapMode}'");

            #region Internal

            // 使えない理由の判定は DescribeGeneratedMetaProblem と共有する。例外にせず先に弾きたい呼び出し側（バグ報告の再生）と同じ基準で読む
            // The unusable-input check is shared with DescribeGeneratedMetaProblem, so callers refusing up front (bug-report replay) read by the same rule
            GeneratedTerrainTransferMeta ReadGenerated()
            {
                var problem = DescribeGeneratedMetaProblem(worldMeta);
                if (problem != null)
                    throw new InvalidOperationException($"Generated world.json '{worldDataDirectory.WorldMetaFilePath}' cannot be used: {problem} Delete the world directory and generate the world again.");

                var origins = new TerrainOrigins(
                    noiseOrigin: new Vector2(worldMeta.TerrainNoiseOriginX.Value, worldMeta.TerrainNoiseOriginZ.Value),
                    sceneOrigin: new Vector2(worldMeta.TerrainSceneOriginX.Value, worldMeta.TerrainSceneOriginZ.Value));
                var payload = new GeneratedTerrainTransferPayload(origins, worldMeta.GenerationMasterFingerprint, worldMeta.GeneratorVersion, worldMeta.PlacementLedgerDigest);
                return new GeneratedTerrainTransferMeta(
                    CalculateGeneratedWorldId(worldMeta), worldMeta.TerrainResolution, worldMeta.TerrainTileCount, CalculateChunkTotal(), worldMeta.Seed, payload);
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

        // 生成ワールドの world.json が読めない理由。読めれば null。Read はこれを例外にし、例外にせず先に弾きたい呼び出し側（バグ報告の再生）は理由として使う
        // Why a generated world's world.json cannot be read, or null when it can; Read throws it, and callers refusing up front (bug-report replay) use it as the reason
        public static string DescribeGeneratedMetaProblem(WorldMetaJson worldMeta)
        {
            if (!WorldMapMode.IsGenerated(worldMeta.MapMode)) return $"mapMode '{worldMeta.MapMode}' is not '{WorldMapMode.Generated}'.";

            // 旧バージョンの転送ファイル構成は新クライアントが読めないので、generatorVersion 不一致は明示拒否する
            // An older transfer layout is unreadable by a new client, so a generatorVersion mismatch is refused explicitly
            if (worldMeta.GeneratorVersion != WorldGeneratorVersion.Current)
                return $"It was written by generator '{worldMeta.GeneratorVersion}', but this build is '{WorldGeneratorVersion.Current}'. " +
                       "The transferred terrain file layout changed (placementLedgerDigest now identifies the placement-dependent visual cache).";

            // 指紋・台帳の指紋・原点は生成時にしか決まらず補えない。0や空で読み進めると別の場所の地形や鍵になる
            // The fingerprint, ledger digest and origins exist only at generation and cannot be filled in; reading on with 0 or empty yields another place's terrain or keys
            if (string.IsNullOrEmpty(worldMeta.GenerationMasterFingerprint)) return "It has no generationMasterFingerprint key, so it predates the generation master fingerprint.";
            if (string.IsNullOrEmpty(worldMeta.PlacementLedgerDigest)) return "It has no placementLedgerDigest key, so it predates the ledger digest transfer.";
            if (worldMeta.TerrainNoiseOriginX == null || worldMeta.TerrainNoiseOriginZ == null || worldMeta.TerrainSceneOriginX == null || worldMeta.TerrainSceneOriginZ == null)
                return "It has no terrain origin keys (terrainNoiseOriginX/Z, terrainSceneOriginX/Z), so it predates the origin transfer.";
            return null;
        }

        // 生成ワールドの worldId は生成入力（seed・指紋・生成器版）から導く。導出の入口を読み手と再生で共有する
        // A generated world's id derives from its generation inputs (seed, fingerprint, generator version); the reader and replay share this entry
        public static string CalculateGeneratedWorldId(WorldMetaJson worldMeta)
        {
            return WorldIdentity.CalculateGenerated(worldMeta.Seed, worldMeta.GenerationMasterFingerprint, worldMeta.GeneratorVersion);
        }
    }
}
