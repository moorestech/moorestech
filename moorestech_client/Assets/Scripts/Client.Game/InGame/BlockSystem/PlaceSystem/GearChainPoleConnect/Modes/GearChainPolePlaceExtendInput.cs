using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
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
        public PlaceInfo GhostPlaceInfo;
        public bool GhostGroundClear;
        public Vector3 GhostCenter;

        // 起点情報（SourcePole != null のときのみ有効）
        // Source pole info (valid only when SourcePole is not null)
        public Vector3Int SourcePolePos;
        public Vector3 SourcePoleCenter;
        public GearChainPoleExtendPreviewData ExtendPreview;

        public int PoleInventorySlot;
        public ItemId OwnedChainItemId;
        public int MaxConnectionCount;
    }
}
