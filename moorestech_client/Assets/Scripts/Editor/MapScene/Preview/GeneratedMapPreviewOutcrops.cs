#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Map.Outcrop;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Map.Interface.Json;
using UnityEditor;
using UnityEngine;

namespace Client.MapScene.Editor
{
    internal static class GeneratedMapPreviewOutcrops
    {
        internal static async UniTask<int> PlaceAsync(IReadOnlyList<MapVeinInfoJson> veins, EditorTerrainAssetLoader assets, Transform parent, CancellationToken cancellationToken)
        {
            var prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            var createdCount = 0;
            for (var index = 0; index < veins.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var info = veins[index];
                var prefab = await ResolvePrefabAsync(info);
                cancellationToken.ThrowIfCancellationRequested();
                if (prefab != null && InstantiateOutcrop(info, prefab)) createdCount++;

                // ランタイムと同じ50件単位で応答し、Closeを次の更新前にも観測する
                // Yield every fifty instances as runtime does and observe Close even before the next update
                if ((index + 1) % 50 != 0) continue;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken, true);
                cancellationToken.ThrowIfCancellationRequested();
            }
            return createdCount;

            #region Internal

            async UniTask<GameObject> ResolvePrefabAsync(MapVeinInfoJson info)
            {
                var element = MasterHolder.MapVeinMaster.GetElementOrNull(info.VeinGuid);
                if (element == null)
                {
                    Debug.LogError($"[GeneratedMapPreview] Missing vein master. VeinGuid:{info.VeinGuid}");
                    return null;
                }
                var address = element.OutcropAddressablePath;
                if (prefabs.TryGetValue(address, out var cached)) return cached;
                GameObject prefab;
                try
                {
                    prefab = await assets.LoadAsync<GameObject>(address, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // AssetDatabaseの外部資産境界。失敗をキャッシュし、各配置は欠損へ残す
                    // AssetDatabase is an external asset boundary; cache failures while leaving every placement missing
                    Debug.LogError($"[GeneratedMapPreview] Outcrop prefab unavailable. VeinGuid:{info.VeinGuid} Address:{address}\n{exception}");
                    prefab = null;
                }
                prefabs.Add(address, prefab);
                return prefab;
            }

            bool InstantiateOutcrop(MapVeinInfoJson info, GameObject prefab)
            {
                GameObject instance;
                try
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                }
                catch (Exception exception)
                {
                    // Prefabの展開も外部資産境界。失敗理由を記録し、この配置を欠損へ残す
                    // Prefab expansion is also an external asset boundary; log the failure and leave this placement missing
                    Debug.LogError($"[GeneratedMapPreview] Outcrop placement failed. VeinGuid:{info.VeinGuid}\n{exception}");
                    return false;
                }
                if (instance == null)
                {
                    Debug.LogError($"[GeneratedMapPreview] Outcrop placement returned no instance. VeinGuid:{info.VeinGuid}");
                    return false;
                }

                // ランタイムの内包AABB中心・無回転・Prefab倍率を保ち、採掘系は初期化しない
                // Preserve runtime's inclusive AABB center, identity rotation, and prefab scale without initializing mining
                var center = new Vector3((info.MinX + info.MaxX + 1) * 0.5f, (info.MinY + info.MaxY + 1) * 0.5f, (info.MinZ + info.MaxZ + 1) * 0.5f);
                instance.transform.SetPositionAndRotation(center, Quaternion.identity);
                instance.name = $"{OutcropGameObjectDatastore.OutcropObjectNamePrefix}{info.VeinGuidStr}";
                return true;
            }

            #endregion
        }
    }
}
#endif
