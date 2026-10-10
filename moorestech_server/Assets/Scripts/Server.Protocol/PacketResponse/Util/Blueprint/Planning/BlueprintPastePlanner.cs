using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Blueprint;
using Game.Construction;

namespace Server.Protocol.PacketResponse.Util.Blueprint.Planning
{
    public static class BlueprintPastePlanner
    {
        public static BlueprintPastePlan Plan(BlueprintJsonObject blueprint, IReadOnlyList<BlueprintPasteOrigin> origins, int rotationStep, IBlueprintPasteWorld world, ConstructionWalletQuery wallet, IReadOnlyDictionary<ItemId, int> heldByItem)
        {
            var isPaymentWaived = world.IsPaymentWaived;
            var materials = new BlueprintPasteMaterialAccumulator(wallet, isPaymentWaived);
            var occupied = new List<BlueprintPasteCopyOccupancy>();
            var copies = new List<BlueprintPasteCopyPlan>();
            var shortageRequirements = new List<(ItemId itemId, int held, int required)>();
            var isShort = false;
            bool? isUnlocked = null;

            // 先行BPの占有を後続判定へ反映
            // Include accepted copy occupancy in later judgements.
            foreach (var origin in origins)
            {
                var draft = BlueprintPasteCopyBuilder.Build(blueprint, origin, rotationStep, world, occupied, out var occupancy);
                var state = JudgePlacementState(draft);
                if (state == BlueprintPasteCopyState.Placeable) state = JudgeMaterials(draft);

                // 状態確定後だけ予約し、不変の結果を作る
                // Reserve only accepted copies and construct an immutable final result
                if (state == BlueprintPasteCopyState.Placeable)
                    occupied.Add(occupancy.SelectPlaced(draft.NonOverlapFlags));
                copies.Add(new BlueprintPasteCopyPlan(draft, state));
            }
            return new BlueprintPastePlan(copies, isPaymentWaived, shortageRequirements);

            #region Internal

            BlueprintPasteCopyState JudgePlacementState(BlueprintPasteCopyDraft draft)
            {
                if (!draft.AreCoordinatesValid) return BlueprintPasteCopyState.InvalidCoordinates;
                if (draft.Elements.Count == 0) return BlueprintPasteCopyState.NoResolvedBlocks;
                if (!draft.IsGroundFound) return BlueprintPasteCopyState.GroundNotFound;
                if (draft.NonOverlapFlags.All(flag => !flag)) return BlueprintPasteCopyState.AllOverlapped;
                // 解放状態は原点に依存せず、最初の適格コピーで一度だけ調べる
                // Unlock state is origin-independent, so inspect it once for the first eligible copy
                if (!isUnlocked.HasValue) isUnlocked = IsUnlocked(draft);
                if (!isUnlocked.Value) return BlueprintPasteCopyState.NotUnlocked;
                return BlueprintPasteCopyState.Placeable;
            }

            bool IsUnlocked(BlueprintPasteCopyDraft draft)
            {
                // 重なりで省略される要素も解放を免除しない
                // Unlock is required even for entries skipped due to overlaps
                foreach (var element in draft.Elements)
                {
                    if (!world.IsBlockUnlocked(blueprint.Blocks[element.BlockIndex].BlockGuid)) return false;
                }

                // マスタに無い線種は復元対象にならず解放判定からも外す
                // Unknown line tools cannot be restored and do not participate in unlock checks
                var resolvedIndices = draft.Elements.Select(element => element.BlockIndex).ToHashSet();
                return blueprint.Wires.Concat(blueprint.Chains)
                    .Where(line => resolvedIndices.Contains(line.BlockIndexA) && resolvedIndices.Contains(line.BlockIndexB))
                    .Where(line => MasterHolder.ConnectToolMaster.GetElementOrNull(line.ConnectToolGuid) != null)
                    .All(line => world.IsConnectToolUnlocked(line.ConnectToolGuid));
            }

            BlueprintPasteCopyState JudgeMaterials(BlueprintPasteCopyDraft draft)
            {
                if (isShort) return BlueprintPasteCopyState.MaterialShortage;

                // 新しいコピーの素材だけを累積し、過去のコピーは再走査しない
                // Accumulate only this copy's materials without rescanning earlier copies
                materials.AddDraft(draft);
                var requirements = ConstructionMaterialAccounting.MatchRequirements(
                    materials.GetRequiredItems(), heldByItem);
                if (requirements.All(row => row.required <= row.held)) return BlueprintPasteCopyState.Placeable;

                // 最初の不足以降はBP単位で拒否する
                // Reject whole copies from the first shortage onward
                isShort = true;
                shortageRequirements.AddRange(requirements);
                return BlueprintPasteCopyState.MaterialShortage;
            }

            #endregion
        }
    }
}
