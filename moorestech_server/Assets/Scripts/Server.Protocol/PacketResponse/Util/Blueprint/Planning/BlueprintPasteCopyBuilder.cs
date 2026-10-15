using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Blueprint;
using Server.Protocol.PacketResponse.Util.ConnectTool;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.Blueprint.Planning
{
    public static class BlueprintPasteCopyBuilder
    {
        internal static BlueprintPasteCopyDraft Build(BlueprintJsonObject blueprint, BlueprintPasteOrigin origin,
            int rotationStep, IBlueprintPasteWorld world, IReadOnlyList<BlueprintPasteCopyOccupancy> accepted, out BlueprintPasteCopyOccupancy occupancy)
        {
            var coordinatesValid = BlueprintPasteCalculator.TryCalculatePlacements(blueprint, origin.Position, rotationStep, out var elements);
            occupancy = BlueprintPasteCopyOccupancy.Create(elements);
            if (!coordinatesValid)
                return new BlueprintPasteCopyDraft(origin.Position, origin.IsGroundFound, elements, new List<BlueprintPasteElementBlockCause>(),
                    elements.Select(_ => false).ToList(), new List<BlueprintPasteLine>(), 0, 0, 0, false);
            var blockCauses = new List<BlueprintPasteElementBlockCause>(elements.Count);
            var terrainSunk = new List<bool>(elements.Count);
            var intersecting = occupancy.FindIntersecting(accepted);
            foreach (var position in occupancy.Positions)
            {
                // 重なり原因を優先して表示しつつ、埋まりの事実は独立して残す
                // Prefer overlap for display while retaining terrain sink independently
                var isTerrainSunk = world.IsTerrainSunk(position);
                terrainSunk.Add(isTerrainSunk);
                blockCauses.Add(ResolveCause(position, isTerrainSunk));
            }
            return Create(blueprint, origin, elements, blockCauses, terrainSunk);

            #region Internal

            BlueprintPasteElementBlockCause ResolveCause(BlockPositionInfo position, bool isTerrainSunk)
            {
                if (world.IsOverlapping(position) || OverlapsAccepted(position)) return BlueprintPasteElementBlockCause.ExistingBlock;
                if (isTerrainSunk) return BlueprintPasteElementBlockCause.TerrainSunk;
                return BlueprintPasteElementBlockCause.None;
            }

            bool OverlapsAccepted(BlockPositionInfo position)
            {
                // 外接箱の空白は予約せず、実際に置くブロックの占有箱だけを見る
                // Reserve actual block footprints rather than empty space in a copy's bounds
                foreach (var occupied in intersecting)
                {
                    if (occupied.Overlaps(position)) return true;
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
            return Create(blueprint, new BlueprintPasteOrigin(Vector3Int.zero, true), elements,
                elements.Select(_ => BlueprintPasteElementBlockCause.None).ToList(), elements.Select(_ => false).ToList());
        }

        private static BlueprintPasteCopyDraft Create(BlueprintJsonObject blueprint, BlueprintPasteOrigin origin,
            List<BlueprintPlacementElement> elements, List<BlueprintPasteElementBlockCause> blockCauses, List<bool> terrainSunk)
        {
            // BP内indexから要素indexを引けるようにする（マスタ欠損ブロックは要素に無い）
            // Map blueprint block indices to element indices (blocks missing from the master have no element)
            var elementIndexByBlockIndex = new Dictionary<int, int>();
            for (var i = 0; i < elements.Count; i++) elementIndexByBlockIndex[elements[i].BlockIndex] = i;

            var lines = new List<BlueprintPasteLine>();
            var evaluator = new BlueprintPasteLineEvaluator();
            var missingEndpointLineCount = 0;
            var blockedEndpointLineCount = 0;
            var unknownConnectToolLineCount = 0;
            ResolveLines(BlueprintPasteLineKind.ElectricWire, blueprint.Wires);
            ResolveLines(BlueprintPasteLineKind.GearChain, blueprint.Chains);
            return new BlueprintPasteCopyDraft(origin.Position, origin.IsGroundFound, elements, blockCauses, terrainSunk, lines,
                missingEndpointLineCount, blockedEndpointLineCount, unknownConnectToolLineCount, true);

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

                    // 片端が配置不可の線は張らない（ADR 0077 決定4: 両端が置けた線だけ）
                    // Skip lines whose endpoint is blocked by overlap or terrain (ADR 0077 decision 4)
                    if (!BlueprintPasteCopyDraft.IsPlaceableCause(blockCauses[indexA]) || !BlueprintPasteCopyDraft.IsPlaceableCause(blockCauses[indexB]))
                    {
                        blockedEndpointLineCount++;
                        continue;
                    }

                    var positionA = elements[indexA].Position;
                    var positionB = elements[indexB].Position;
                    if (!ConnectToolCostCalculator.TryCalculate(saved.ConnectToolGuid, Vector3Int.Distance(positionA, positionB), out var materials))
                    {
                        unknownConnectToolLineCount++;
                        continue;
                    }

                    lines.Add(new BlueprintPasteLine(kind, indexA, indexB, positionA, positionB, saved.ConnectToolGuid, materials,
                        evaluator.Evaluate(kind, elements[indexA], elements[indexB])));
                }
            }

            #endregion
        }
    }
}
