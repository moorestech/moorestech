using System;
using System.IO;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Newtonsoft.Json;

namespace Server.Boot.Replay.World
{
    // 生成ワールドの world.json を読み、再生の候補が箱と同じワールドかを確かめる。必須キーの判定は TerrainTransferMetaReader と共有する
    // Reads a generated world's world.json and checks a replay candidate is the box's own world; the required-key rule is shared with TerrainTransferMetaReader
    public static class BugReportGeneratedWorldMetaCheck
    {
        // 候補を再生に使えない理由。使えれば null。置き場の名前（worldId）と中身、原点を箱と照合し、タイルが読み手の列挙どおり揃うかを見る
        // Why a candidate cannot be used for replay, or null; checks its directory name (worldId), contents and origins against the box, and its tiles against the reader's enumeration
        public static string FindCandidateProblem(WorldDataDirectory candidate, WorldMetaJson candidateMeta, WorldMetaJson bundleMeta, string expectedWorldId)
        {
            var metaProblem = TerrainTransferMetaReader.DescribeGeneratedMetaProblem(candidateMeta);
            if (metaProblem != null) return $"world.json を使えない: {metaProblem}";

            // 置き場の名前だけを信じると、別ワールドが同名で置かれていても引き当ててしまう。中身から worldId を導き直して照合する
            // Trusting the directory name alone would pick up another world stored under the same name, so the worldId is re-derived from the contents
            var candidateWorldId = TerrainTransferMetaReader.CalculateGeneratedWorldId(candidateMeta);
            if (candidateWorldId != expectedWorldId) return $"world.json から導いた worldId（{candidateWorldId}）が箱の worldId（{expectedWorldId}）と一致しない";

            // 原点は worldId の入力に含まれないが、ずれると map.json の座標と地形の位置が食い違う
            // The origins do not feed the worldId, yet a shift misaligns map.json coordinates with the terrain
            if (candidateMeta.TerrainNoiseOriginX != bundleMeta.TerrainNoiseOriginX || candidateMeta.TerrainNoiseOriginZ != bundleMeta.TerrainNoiseOriginZ ||
                candidateMeta.TerrainSceneOriginX != bundleMeta.TerrainSceneOriginX || candidateMeta.TerrainSceneOriginZ != bundleMeta.TerrainSceneOriginZ)
                return $"地形原点が箱の world.json と一致しない（箱 noise:{bundleMeta.TerrainNoiseOriginX},{bundleMeta.TerrainNoiseOriginZ} scene:{bundleMeta.TerrainSceneOriginX},{bundleMeta.TerrainSceneOriginZ} / " +
                       $"候補 noise:{candidateMeta.TerrainNoiseOriginX},{candidateMeta.TerrainNoiseOriginZ} scene:{candidateMeta.TerrainSceneOriginX},{candidateMeta.TerrainSceneOriginZ}）";
            return FindTerrainTileProblem();

            #region Internal

            // 読み手はタイル数から全タイルの height を列挙し長さを読む（CalculateChunkTotal）。数が正の平方数でない・1枚でも欠けると例外になる
            // The reader enumerates every tile's height from the tile count and reads its length (CalculateChunkTotal); a non-square count or any missing tile throws
            string FindTerrainTileProblem()
            {
                var tileCountProblem = TerrainTransferMeta.DescribeTileCountProblem(candidateMeta.TerrainTileCount);
                if (tileCountProblem != null) return $"terrainTileCount（{candidateMeta.TerrainTileCount}）で地形タイルを並べられない: {tileCountProblem}";
                foreach (var tilePath in TerrainTransferMeta.EnumerateStreamFilePaths(candidate, candidateMeta.TerrainTileCount))
                {
                    if (!File.Exists(tilePath)) return $"terrainTileCount（{candidateMeta.TerrainTileCount}）の地形タイル {tilePath} が無い";
                }
                return null;
            }

            #endregion
        }

        // world.json は外部入力のファイルとJSON（ファイルI/O・権限とJSONパースの境界）。読めない理由を返し、呼び出し側が拒否理由やログに載せる
        // world.json is external file + JSON input (file I/O and JSON parse boundary); the reason is returned for the caller's rejection or log
        public static WorldMetaJson ReadMeta(string path, out string error)
        {
            error = null;
            try
            {
                var meta = JsonConvert.DeserializeObject<WorldMetaJson>(File.ReadAllText(path));
                if (meta == null) error = "world.json が空です";
                return meta;
            }
            catch (JsonException e)
            {
                error = e.Message;
                return null;
            }
            catch (IOException e)
            {
                error = e.Message;
                return null;
            }
            catch (UnauthorizedAccessException e)
            {
                error = e.Message;
                return null;
            }
        }
    }
}
