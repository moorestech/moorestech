using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Blueprint;
using Server.Protocol.PacketResponse.Util.ConnectTool;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.Blueprint.Planning
{
    public static class BlueprintPasteCopyBuilder
    {
        internal static BlueprintPasteCopyDraft Build(BlueprintJsonObject blueprint, BlueprintPasteOrigin origin,
            int rotationStep, IBlueprintPasteWorld world, IReadOnlyList<BlueprintPasteCopyDraft> accepted)
        {
            var elements = BlueprintPasteCalculator.CalculatePlacements(blueprint, origin.Position, rotationStep);
            var nonOverlapFlags = new List<bool>(elements.Count);
            foreach (var element in elements)
            {
                var position = BlueprintPlacementElementUtil.ToPositionInfo(element);
                nonOverlapFlags.Add(!world.IsOverlapping(position) && !OverlapsAccepted(position));
            }
            return Create(blueprint, origin, elements, nonOverlapFlags);

            #region Internal

            bool OverlapsAccepted(BlockPositionInfo position)
            {
                // 外接箱の空白は予約せず、実際に置くブロックの占有箱だけを見る
                // Reserve actual block footprints rather than empty space in a copy's bounds
                foreach (var copy in accepted)
                {
                    for (var i = 0; i < copy.Elements.Count; i++)
                    {
                        if (!copy.NonOverlapFlags[i]) continue;
                        var occupied = BlueprintPlacementElementUtil.ToPositionInfo(copy.Elements[i]);
                        if (position.IsOverlap(occupied))
                            return true;
                    }
                }
                return false;
            }

            #endregion
        }

        // ビルドメニュー用: 回転0・原点0・重なりなしのBP1個
        // For the build menu: one copy at rotation 0 and origin 0 with nothing overlapping
        public static BlueprintPasteCopyDraft BuildUnobstructed(BlueprintJsonObject blueprint)
        {
            var elements = BlueprintPasteCalculator.CalculatePlacements(blueprint, Vector3Int.zero, 0);
            return Create(blueprint, new BlueprintPasteOrigin(Vector3Int.zero, true), elements, elements.Select(_ => true).ToList());
        }

        private static BlueprintPasteCopyDraft Create(BlueprintJsonObject blueprint, BlueprintPasteOrigin origin, List<BlueprintPlacementElement> elements, List<bool> nonOverlapFlags)
        {
            // BP内indexから要素indexを引けるようにする（マスタ欠損ブロックは要素に無い）
            // Map blueprint block indices to element indices (blocks missing from the master have no element)
            var elementIndexByBlockIndex = new Dictionary<int, int>();
            for (var i = 0; i < elements.Count; i++) elementIndexByBlockIndex[elements[i].BlockIndex] = i;

            var lines = new List<BlueprintPasteLine>();
            var missingEndpointLineCount = 0;
            var overlappingEndpointLineCount = 0;
            var unknownConnectToolLineCount = 0;
            ResolveLines(BlueprintPasteLineKind.ElectricWire, blueprint.Wires);
            ResolveLines(BlueprintPasteLineKind.GearChain, blueprint.Chains);
            return new BlueprintPasteCopyDraft(origin.Position, origin.IsGroundFound, elements, nonOverlapFlags, lines,
                missingEndpointLineCount, overlappingEndpointLineCount, unknownConnectToolLineCount);

            #region Internal

            void ResolveLines(BlueprintPasteLineKind kind, List<BlueprintLineJsonObject> savedLines)
            {
                foreach (var saved in savedLines)
                {
                    // 端点ブロックがマスタから消えた線は張れない
                    // A line whose endpoint block vanished from the master cannot be drawn
                    if (!elementIndexByBlockIndex.TryGetValue(saved.BlockIndexA, out var indexA) || !elementIndexByBlockIndex.TryGetValue(saved.BlockIndexB, out var indexB))
                    {
                        missingEndpointLineCount++;
                        continue;
                    }

                    // 片端が重なりで置けない線は張らない（ADR 0077 決定4: 両端が置けた線だけ）
                    // Skip lines whose endpoint is blocked by an overlap (ADR 0077 decision 4)
                    if (!nonOverlapFlags[indexA] || !nonOverlapFlags[indexB])
                    {
                        overlappingEndpointLineCount++;
                        continue;
                    }

                    var positionA = elements[indexA].Position;
                    var positionB = elements[indexB].Position;
                    if (!ConnectToolCostCalculator.TryCalculate(saved.ConnectToolGuid, Vector3Int.Distance(positionA, positionB), out var materials))
                    {
                        unknownConnectToolLineCount++;
                        continue;
                    }

                    lines.Add(new BlueprintPasteLine(kind, indexA, indexB, positionA, positionB, saved.ConnectToolGuid, materials));
                }
            }

            #endregion
        }
    }
}
