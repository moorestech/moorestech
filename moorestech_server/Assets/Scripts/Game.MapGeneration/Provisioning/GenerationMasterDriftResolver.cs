using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Core.Master;
using Game.Map.Interface.Json;
using Game.MapGeneration.Export;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.MapGeneration.Provisioning
{
    /// <summary>
    ///     ワールド作成時から生成マスタが動いたときの解決。動いたのが見た目だけならワールドは作り直させない。
    ///     pass-1を回し直して(mapObjectGuid,シーン座標)集合が保たれているかを見て、保たれていれば共有キャッシュの見た目だけを捨てる
    ///     Resolves a generation master that moved since world creation; a move confined to visuals never forces recreating the world.
    ///     pass-1 is re-run to see whether the (mapObjectGuid, scene position) set still holds, and if it does only the shared cache's visuals are dropped
    /// </summary>
    public static class GenerationMasterDriftResolver
    {
        // 位置はfloatでmap.jsonを往復するので1mm刻みへ丸めて突き合わせる。配置が実際に動くときはメートル単位で移る
        // Positions round-trip through map.json as floats, so the keys quantise to 1mm; a placement that really moved shifts by metres
        private const int PositionKeyUnitsPerMeter = 1000;

        // scaleは配置台帳digestと同じくfloatの全精度を保つ。位置の1mm許容を流用すると小さな見た目差を同一配置と誤認する
        // Scale keeps full float precision as the placement-ledger digest does; reusing position's 1mm tolerance would hide small visual changes
        private const string ScaleRoundTripFormat = "R";

        // 生成専用値だけを見るのでgenerated限定で受ける。templateを渡す経路は型で塞ぐ
        // It reads generated-only values, so it takes the generated meta alone and the type forbids handing it a template
        public static void Resolve(WorldDataDirectory worldDataDirectory, string serverDataDirectory, GeneratedTerrainTransferMeta terrainMeta)
        {
            // ここは指紋を突き合わせるだけでなく、進める新しい値としても要るので算出結果を手元に持つ
            // The fingerprint is needed here not only to match but as the new value to advance to, so the computation is kept at hand
            var generatedPayload = terrainMeta.GeneratedPayload;
            var currentFingerprint = generatedPayload.ComputeCurrentGenerationMasterFingerprint(serverDataDirectory);
            if (currentFingerprint == generatedPayload.GenerationMasterFingerprint) return;

            // クライアントのOpenと同じ組み立てでpass-1を回し直す。ここを通れば、クライアントも同じ台帳へ辿り着く
            // Re-run pass-1 through the very assembly the client's Open uses, so passing here means the client reaches the same ledger
            var selectedGeneration = MasterHolder.GenerationMaster.SelectedGeneration;
            var config = MapGenerationPipeline.BuildConfigWithSettledOrigins(
                selectedGeneration, terrainMeta.WorldSeed, serverDataDirectory, generatedPayload.Origins);
            var run = MapGenerationPipeline.Generate(selectedGeneration, config);

            // 窓が動いていたら配置以前に別のワールドなので、集合の比較より先に止める
            // A moved window is a different world before any placement is compared, so stop ahead of the set comparison
            generatedPayload.ThrowIfOriginsDiffer(new TerrainOrigins(run.Output.NoiseOrigin, run.Output.SceneOrigin));
            ThrowIfMapObjectsMoved(worldDataDirectory, run.Output);

            DropSharedVisualCache(terrainMeta.WorldId);
            AdvanceRecordedFingerprint(worldDataDirectory, currentFingerprint, run.Ledger.ComputeDigest());
            Debug.Log(
                $"[GenerationMasterDriftResolver] World '{terrainMeta.WorldId}' kept its placements across a generation master change; " +
                "its shared visual cache was dropped and will be rebuilt.");

            #region Internal

            // 台帳から焼く見た目(岩周辺テクスチャ等)は配置に貼り付く。map.jsonが記録した集合と食い違う台帳で焼くと、無い岩の周りを塗る
            // The visuals baked from the ledger (rock surrounds and the like) stick to placements; baking from a ledger disagreeing with the set map.json recorded would paint around absent rocks
            // 配置は高さと分類から導かれるので、転送済みの高さが別物になるマスタ変更はこの集合も動かす。集合一致は高さ据え置きの代理でもある
            // Placements derive from the heights and the classification, so a master change that would make the transferred heights another terrain moves this set too: set equality doubles as a proxy for the heights still fitting
            static void ThrowIfMapObjectsMoved(WorldDataDirectory worldDataDirectory, MapGenerationOutput output)
            {
                var recordedMapInfo = JsonConvert.DeserializeObject<MapInfoJson>(File.ReadAllText(worldDataDirectory.MapJsonFilePath));
                var recordedKeys = SortedPlacementKeys(recordedMapInfo.MapObjects.Select(
                    mapObject => PlacementKey(mapObject.MapObjectGuidStr, mapObject.Position, mapObject.Scale)));
                var regeneratedKeys = SortedPlacementKeys(output.MapObjects.Select(
                    mapObject => PlacementKey(mapObject.MapObjectGuid, mapObject.Position, mapObject.Scale)));
                if (recordedKeys.SequenceEqual(regeneratedKeys)) return;

                throw new InvalidOperationException(
                    $"The generation master moved the placements of world '{worldDataDirectory.Root}': map.json records {recordedKeys.Count} " +
                    $"map objects while the current master generates {regeneratedKeys.Count} with a different (guid, position, scale) set. " +
                    "Delete the world directory and generate the world again.");
            }

            static List<string> SortedPlacementKeys(IEnumerable<string> keys)
            {
                return keys.OrderBy(key => key, StringComparer.Ordinal).ToList();
            }

            static string PlacementKey(string mapObjectGuid, Vector3 position, Vector3 scale)
            {
                return $"{mapObjectGuid}:{RoundToKeyUnits(position.x)}:{RoundToKeyUnits(position.y)}:{RoundToKeyUnits(position.z)}:" +
                       $"{FormatScale(scale.x)}:{FormatScale(scale.y)}:{FormatScale(scale.z)}";
            }

            static int RoundToKeyUnits(float meters)
            {
                return Mathf.RoundToInt(meters * PositionKeyUnitsPerMeter);
            }

            static string FormatScale(float scale)
            {
                return scale.ToString(ScaleRoundTripFormat, CultureInfo.InvariantCulture);
            }

            // 見た目キャッシュは指紋を鍵に含むので放置しても引かれないが、二度と使えないファイルは残さない
            // The visual cache keys on the fingerprint so stale files would never be hit, yet files that can never be used again are not left behind
            static void DropSharedVisualCache(string worldId)
            {
                var sharedVisualDirectory = WorldDataDirectory.ForWorldCache(worldId).TerrainVisualDirectory;
                if (Directory.Exists(sharedVisualDirectory)) Directory.Delete(sharedVisualDirectory, true);
            }

            // world.jsonはコミットマーカーなので上書きしない。プロビジョニングと同じ一時ディレクトリへ書いてから置換する
            // world.json is the commit marker and is never overwritten in place: it is written into provisioning's own temp directory and then replaced
            static void AdvanceRecordedFingerprint(
                WorldDataDirectory worldDataDirectory, string currentFingerprint, string currentPlacementLedgerDigest)
            {
                var worldMeta = JsonConvert.DeserializeObject<WorldMetaJson>(File.ReadAllText(worldDataDirectory.WorldMetaFilePath));
                worldMeta.GenerationMasterFingerprint = currentFingerprint;

                // 見た目が動いた台帳は指紋も動く。指紋を据え置くと、捨てたキャッシュを新しい見た目で焼き直せない
                // A ledger whose visuals moved has a moved digest too; holding it back would keep the dropped cache from being rebaked with the new look
                worldMeta.PlacementLedgerDigest = currentPlacementLedgerDigest;

                var temporaryDataDirectory = WorldDataDirectory.FromWorldRoot(worldDataDirectory.ProvisioningTempDirectory);
                Directory.CreateDirectory(temporaryDataDirectory.Root);
                File.WriteAllText(temporaryDataDirectory.WorldMetaFilePath, JsonConvert.SerializeObject(worldMeta, Formatting.Indented));
                File.Replace(temporaryDataDirectory.WorldMetaFilePath, worldDataDirectory.WorldMetaFilePath, null);
                Directory.Delete(temporaryDataDirectory.Root, true);
            }

            #endregion
        }
    }
}
