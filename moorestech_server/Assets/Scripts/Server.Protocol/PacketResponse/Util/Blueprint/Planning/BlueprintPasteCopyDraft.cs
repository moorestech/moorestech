using System;
using System.Collections.Generic;
using System.Linq;
using Game.Blueprint;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.Blueprint.Planning
{
    public class BlueprintPasteCopyDraft
    {
        public Vector3Int Origin { get; }
        public bool IsGroundFound { get; }
        public IReadOnlyList<BlueprintPlacementElement> Elements { get; }
        public IReadOnlyList<bool> NonOverlapFlags { get; }
        public IReadOnlyList<BlueprintPasteLine> Lines { get; }

        // プレビューは無音で判定し、実行側が省略理由をまとめて報告する
        // Keep preview planning silent and let execution report aggregated skip reasons
        public int MissingEndpointLineCount { get; }
        public int OverlappingEndpointLineCount { get; }
        public int UnknownConnectToolLineCount { get; }

        public BlueprintPasteCopyDraft(Vector3Int origin, bool isGroundFound, IReadOnlyList<BlueprintPlacementElement> elements,
            IReadOnlyList<bool> nonOverlapFlags, IReadOnlyList<BlueprintPasteLine> lines,
            int missingEndpointLineCount, int overlappingEndpointLineCount, int unknownConnectToolLineCount)
        {
            Origin = origin;
            IsGroundFound = isGroundFound;

            // 呼び出し側のリスト変更で判定結果を変えない
            // Snapshot lists so callers cannot change a completed judgement
            Elements = Array.AsReadOnly(elements.ToArray());
            NonOverlapFlags = Array.AsReadOnly(nonOverlapFlags.ToArray());
            Lines = Array.AsReadOnly(lines.ToArray());
            MissingEndpointLineCount = missingEndpointLineCount;
            OverlappingEndpointLineCount = overlappingEndpointLineCount;
            UnknownConnectToolLineCount = unknownConnectToolLineCount;
        }
    }
}
