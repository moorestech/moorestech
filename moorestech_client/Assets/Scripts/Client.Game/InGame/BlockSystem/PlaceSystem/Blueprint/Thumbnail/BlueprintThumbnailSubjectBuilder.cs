using Client.Game.InGame.Context;
using Game.Block.Interface;
using Game.Blueprint;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail
{
    /// <summary>
    ///     BPのブロックプレファブを撮影器の子へ並べる
    ///     Arranges a blueprint's block prefabs under the photographer
    /// </summary>
    public static class BlueprintThumbnailSubjectBuilder
    {
        public static bool TryBuild(BlueprintJsonObject blueprint, Transform parent, out GameObject subject)
        {
            var placements = BlueprintPasteCalculator.CalculatePlacements(blueprint, Vector3Int.zero, 0);
            var missingCount = blueprint.Blocks.Count - placements.Count;
            if (0 < missingCount) Debug.LogWarning($"[BlueprintThumbnail] blueprint {blueprint.BlueprintGuid} skipped {missingCount} blocks missing from the master");
            if (placements.Count == 0)
            {
                Debug.LogError($"[BlueprintThumbnail] blueprint {blueprint.BlueprintGuid} ({blueprint.Name}) has no resolvable blocks; thumbnail skipped");
                subject = null;
                return false;
            }

            subject = new GameObject($"BlueprintThumbnailSubject:{blueprint.Name}");
            subject.transform.SetParent(parent, false);

            // 実設置と同じ座標変換で、撮影器直下にローカル配置する
            // Use real-placement coordinates within the photographer's local space
            foreach (var placement in placements)
            {
                if (!ClientContext.BlockGameObjectPrefabContainer.BlockPrefabInfos.ContainsKey(placement.BlockId))
                {
                    Debug.LogError($"[BlueprintThumbnail] prefab missing for block {placement.BlockId.AsPrimitive()} in blueprint {blueprint.BlueprintGuid}; skipped");
                    continue;
                }

                var position = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(placement.Position, placement.Direction, placement.BlockId);
                var block = ClientContext.BlockGameObjectPrefabContainer.CreateBlockGameObject(placement.BlockId, Vector3.zero, placement.Direction.GetRotation());
                block.transform.SetParent(subject.transform, false);
                block.transform.localPosition = position;
                block.SetActive(true);
            }

            return true;
        }
    }
}
