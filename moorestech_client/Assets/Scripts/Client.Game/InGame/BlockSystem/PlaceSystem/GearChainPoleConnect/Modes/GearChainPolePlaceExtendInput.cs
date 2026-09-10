using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Core.Master;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Modes
{
    /// <summary>
    /// ポールアイテム手持ちモードの1フレーム分の入力スナップショット。環境読み取りはすべてCollectorが済ませている。
    /// Per-frame input snapshot for the pole-item mode. All environment reads are done by the collector beforehand.
    /// </summary>
    public struct GearChainPolePlaceExtendInput
    {
        public IGearChainPoleConnectAreaCollider HitPole;
        public IGearChainPoleConnectAreaCollider SourcePole;
        public bool Clicked;
        public bool IsAwaitingResponse;

        // ゴースト候補（レイ命中かつ設置距離内のときのみ有効）
        // Ghost candidate (valid only when the ray hits within placeable distance)
        public bool HasGhost;

        // 距離超過でゴーストを出さなかった
        // No ghost because the cursor is beyond the placeable distance
        public bool GhostTooFar;

        public PlaceInfo GhostPlaceInfo;
        public bool GhostGroundClear;
        public Vector3 GhostCenter;

        // 賄えないときの不足素材。行にはせず関門へ渡す
        // The shortage when it is not affordable; handed to the gate rather than turned into lines
        public IReadOnlyList<ConstructionMaterialShortage> GhostMaterialShortages;

        // 設置するポール自身の建設コストを賄えるか。不足リストから導き二重保持しない
        // Whether the pole's own construction cost is affordable, derived from the shortage list instead of stored twice
        public bool GhostAffordable => GhostMaterialShortages == null || GhostMaterialShortages.Count == 0;

        // 起点情報（SourcePole != null のときのみ有効）
        // Source pole info (valid only when SourcePole is not null)
        public Vector3Int SourcePolePos;
        public Vector3 SourcePoleCenter;
        public GearChainPoleExtendPreviewData ExtendPreview;

        public BlockId PoleBlockId;
        public Guid ConnectToolGuid;
        public int MaxConnectionCount;
    }
}
