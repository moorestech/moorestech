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
            var accepted = new List<BlueprintPasteCopyDraft>();
            var copies = new List<BlueprintPasteCopyPlan>();
            var shortageRequirements = new List<(ItemId itemId, int held, int required)>();
            var isShort = false;

            // 先行する受理済みBPの占有を後続の判定に含める
            // Include earlier accepted copies in each later overlap judgement
            foreach (var origin in origins)
            {
                var draft = BlueprintPasteCopyBuilder.Build(blueprint, origin, rotationStep, world, accepted);
                var state = JudgePlacementState(draft);
                if (state == BlueprintPasteCopyState.Placeable) state = JudgeMaterials(draft);

                // 状態確定後だけ予約し、不変の結果を作る
                // Reserve only accepted copies and construct an immutable final result
                if (state == BlueprintPasteCopyState.Placeable)
                    accepted.Add(draft);
                copies.Add(new BlueprintPasteCopyPlan(draft, state));
            }
            return new BlueprintPastePlan(copies, isPaymentWaived, shortageRequirements);

            #region Internal

            BlueprintPasteCopyState JudgePlacementState(BlueprintPasteCopyDraft draft)
            {
                if (!draft.IsGroundFound) return BlueprintPasteCopyState.GroundNotFound;
                if (draft.NonOverlapFlags.All(flag => !flag)) return BlueprintPasteCopyState.AllOverlapped;
                if (!IsUnlocked(draft)) return BlueprintPasteCopyState.NotUnlocked;
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
                return blueprint.Wires.Concat(blueprint.Chains)
                    .Where(line => MasterHolder.ConnectToolMaster.GetElementOrNull(line.ConnectToolGuid) != null)
                    .All(line => world.IsConnectToolUnlocked(line.ConnectToolGuid));
            }

            BlueprintPasteCopyState JudgeMaterials(BlueprintPasteCopyDraft draft)
            {
                if (isShort) return BlueprintPasteCopyState.MaterialShortage;

                // 判定中のコピーは占有予約へ入れずに累積素材だけを調べる
                // Check cumulative materials without reserving the candidate's occupied cells
                var candidates = new List<BlueprintPasteCopyDraft>(accepted) { draft };
                var requirements = ConstructionMaterialAccounting.MatchRequirements(
                    BlueprintPasteCostCalculator.CalcRequiredItems(candidates, wallet, isPaymentWaived), heldByItem);
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
