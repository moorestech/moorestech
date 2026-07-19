using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Modes
{
    /// <summary>
    /// チェーンアイテム手持ちモードの1フレーム分の入力スナップショット。環境読み取りはすべてCollectorが済ませている。
    /// Per-frame input snapshot for the chain-item mode. All environment reads are done by the collector beforehand.
    /// </summary>
    public struct GearChainPoleChainConnectInput
    {
        public IGearChainPoleConnectAreaCollider HitPole;
        public IGearChainPoleConnectAreaCollider SourcePole;
        public bool Clicked;
        public Guid ConnectToolGuid;

        // 起点情報（SourcePole != null のときのみ有効）
        // Source pole info (valid only when SourcePole is not null)
        public Vector3Int SourcePolePos;
        public Vector3 SourcePoleCenter;

        // 起点↔命中ポールの接続評価（起点と命中ポールが両方あるときのみ有効）
        // Connection judgement between source and hit pole (valid only when both exist)
        public Vector3Int HitPolePos;
        public GearChainPoleExtendPreviewData PoleToPolePreview;

        // ポール非命中時の赤線終点（レイが地形等に命中したときのみ有効）
        // Red line end point while no pole is hit (valid only when the ray hits terrain etc.)
        public bool HasCursorPoint;
        public Vector3 CursorPoint;
    }
}
