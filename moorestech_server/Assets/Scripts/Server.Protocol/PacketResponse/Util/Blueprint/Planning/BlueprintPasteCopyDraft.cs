using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Blueprint;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.Blueprint.Planning
{
    // BP要素の不可原因は表示と実行で共有する
    // Share element rejection causes between preview and execution
    public enum BlueprintPasteElementBlockCause { None, ExistingBlock, TerrainSunk }

    public class BlueprintPasteCopyDraft
    {
        internal static bool IsPlaceableCause(BlueprintPasteElementBlockCause cause) => cause == BlueprintPasteElementBlockCause.None;
        public bool IsElementPlaceable(int elementIndex) => IsPlaceableCause(ElementBlockCauses[elementIndex]);

        // サーバーへ送る埋まり要素の足場
        // Footprints of terrain-sunk elements sent to the server
        public IEnumerable<BlockPositionInfo> TerrainSunkFootprints()
        {
            for (var i = 0; i < Elements.Count; i++)
                if (ElementBlockCauses[i] == BlueprintPasteElementBlockCause.TerrainSunk)
                    yield return BlueprintPlacementElementUtil.ToPositionInfo(Elements[i]);
        }

        public Vector3Int Origin { get; }
        internal bool IsGroundFound { get; }
        public bool AreCoordinatesValid { get; }
        public IReadOnlyList<BlueprintPlacementElement> Elements { get; }
        public IReadOnlyList<BlueprintPasteElementBlockCause> ElementBlockCauses { get; }
        public IReadOnlyList<BlueprintPasteLine> Lines { get; }

        // プレビューは無音で判定し、実行側が省略理由をまとめて報告する
        // Keep preview planning silent and let execution report aggregated skip reasons
        public int MissingEndpointLineCount { get; }
        public int BlockedEndpointLineCount { get; }
        public int UnknownConnectToolLineCount { get; }

        public BlueprintPasteCopyDraft(Vector3Int origin, bool isGroundFound, IReadOnlyList<BlueprintPlacementElement> elements,
            IReadOnlyList<BlueprintPasteElementBlockCause> elementBlockCauses, IReadOnlyList<BlueprintPasteLine> lines,
            int missingEndpointLineCount, int blockedEndpointLineCount, int unknownConnectToolLineCount, bool areCoordinatesValid)
        {
            Origin = origin;
            AreCoordinatesValid = areCoordinatesValid;
            IsGroundFound = isGroundFound;

            // 呼び出し側のリスト変更で判定結果を変えない
            // Snapshot lists so callers cannot change a completed judgement
            Elements = Array.AsReadOnly(elements.ToArray());
            ElementBlockCauses = Array.AsReadOnly(elementBlockCauses.ToArray());
            Lines = Array.AsReadOnly(lines.ToArray());
            MissingEndpointLineCount = missingEndpointLineCount;
            BlockedEndpointLineCount = blockedEndpointLineCount;
            UnknownConnectToolLineCount = unknownConnectToolLineCount;
        }
    }
}
