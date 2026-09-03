using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    /// <summary>
    /// 新設橋脚を伴うレール接続の判定結果。橋脚の建設コスト不足もレール素材不足と同じ入口で確定する
    /// Judgement for a rail connection that places a new pier; the pier's construction cost shortage is settled at the same entry point as the rail material shortage
    /// </summary>
    public readonly struct TrainRailPierPlacementJudgement
    {
        public readonly RailPlacementJudgement Judgement;
        public readonly IReadOnlyList<ConstructionMaterialShortage> RailMaterialShortages;
        public readonly IReadOnlyList<ConstructionMaterialShortage> PierMaterialShortages;

        // 橋脚コストを賄えるか。サーバーが設置前に行うHasRequiredItemsと同じ関門
        // Whether the pier cost is affordable; the same gate the server applies with HasRequiredItems before placing
        public bool IsPierAffordable => PierMaterialShortages.Count == 0;

        public TrainRailPierPlacementJudgement(RailPlacementJudgement judgement, IReadOnlyList<ConstructionMaterialShortage> railMaterialShortages, IReadOnlyList<ConstructionMaterialShortage> pierMaterialShortages)
        {
            Judgement = judgement;
            RailMaterialShortages = railMaterialShortages;
            PierMaterialShortages = pierMaterialShortages;
        }
    }

    public struct TrainRailConnectPreviewData : IEquatable<TrainRailConnectPreviewData>
    {
        // ノード解決に失敗した状態を表すため、名前付き引数で「無効かつ設置不可」を明示する
        // Represents a node resolution failure, so named arguments state "invalid and not placeable" explicitly
        public static TrainRailConnectPreviewData Invalid => new(
            startPoint: Vector3.zero,
            startControlPoint: Vector3.zero,
            endControlPoint: Vector3.zero,
            endPoint: Vector3.zero,
            railTypeGuid: Guid.Empty,
            isValid: false,
            failureReason: RailConnectionEditProtocol.RailConnectionEditFailureReason.InvalidNode,
            isCurvePlaceable: false,
            materialShortages: Array.Empty<ConstructionMaterialShortage>(),
            pierMaterialShortages: Array.Empty<ConstructionMaterialShortage>());

        public Vector3 StartPoint;
        public Vector3 StartControlPoint;
        public Vector3 EndControlPoint;
        public Vector3 EndPoint;
        public Guid RailTypeGuid;
        public bool IsValid;
        public RailConnectionEditProtocol.RailConnectionEditFailureReason FailureReason;
        public bool IsCurvePlaceable;

        // 素材不足時のみ非空、他は空。FailureReasonに従属する派生値なので等値比較には含めない
        // Non-empty only on a material shortage; it derives from FailureReason so it stays out of the equality comparison
        public IReadOnlyList<ConstructionMaterialShortage> MaterialShortages;

        // 新設橋脚自身の建設コスト不足。空でなければ可否も落ちるため、可否側の真偽だけを等値比較に含める
        // The new pier's own construction cost shortage; it also drops placeability, so only that boolean joins the equality comparison
        public IReadOnlyList<ConstructionMaterialShortage> PierMaterialShortages;
        public bool IsPierAffordable;

        // 可否は失敗理由・カーブ可否・橋脚コストから導出する（「不可なのに理由None」という状態を型で表現不能にする）
        // Placeability is derived from the failure reason, curve placeability and pier cost (makes "blocked with no reason" unrepresentable)
        public bool IsPlaceable => FailureReason == RailConnectionEditProtocol.RailConnectionEditFailureReason.None && IsCurvePlaceable && IsPierAffordable;

        public TrainRailConnectPreviewData(Vector3 startPoint, Vector3 startControlPoint, Vector3 endControlPoint, Vector3 endPoint, RailPlacementJudgement judgement, bool isClientCurvePlaceable, IReadOnlyList<ConstructionMaterialShortage> materialShortages, IReadOnlyList<ConstructionMaterialShortage> pierMaterialShortages)
            : this(startPoint, startControlPoint, endControlPoint, endPoint, judgement.SelectedRailTypeGuid, true, judgement.FailureReason, isClientCurvePlaceable, materialShortages, pierMaterialShortages)
        {
        }

        private TrainRailConnectPreviewData(Vector3 startPoint, Vector3 startControlPoint, Vector3 endControlPoint, Vector3 endPoint, Guid railTypeGuid, bool isValid, RailConnectionEditProtocol.RailConnectionEditFailureReason failureReason, bool isCurvePlaceable, IReadOnlyList<ConstructionMaterialShortage> materialShortages, IReadOnlyList<ConstructionMaterialShortage> pierMaterialShortages)
        {
            StartPoint = startPoint;
            StartControlPoint = startControlPoint;
            EndControlPoint = endControlPoint;
            EndPoint = endPoint;
            IsValid = isValid;
            RailTypeGuid = railTypeGuid;
            FailureReason = failureReason;
            IsCurvePlaceable = isCurvePlaceable;
            MaterialShortages = materialShortages;
            PierMaterialShortages = pierMaterialShortages;
            IsPierAffordable = pierMaterialShortages.Count == 0;
        }

        // MaterialShortagesを含めないのは、RailConnectPreviewObjectが毎フレームの同値スキップにこの比較を使うため（新しいリスト参照で毎回不一致になりメッシュを作り直す）
        // MaterialShortages is excluded because RailConnectPreviewObject uses this comparison to skip identical frames (a fresh list reference would mismatch every frame and rebuild the mesh)
        public bool Equals(TrainRailConnectPreviewData other)
        {
            return StartPoint.Equals(other.StartPoint) && StartControlPoint.Equals(other.StartControlPoint) && EndControlPoint.Equals(other.EndControlPoint) && EndPoint.Equals(other.EndPoint) && RailTypeGuid.Equals(other.RailTypeGuid) && IsValid == other.IsValid && FailureReason == other.FailureReason && IsCurvePlaceable == other.IsCurvePlaceable && IsPierAffordable == other.IsPierAffordable;
        }
        public override bool Equals(object obj)
        {
            return obj is TrainRailConnectPreviewData other && Equals(other);
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(StartPoint, StartControlPoint, EndControlPoint, EndPoint, RailTypeGuid, IsValid, FailureReason, HashCode.Combine(IsCurvePlaceable, IsPierAffordable));
        }
        public override string ToString()
        {
            return $"({StartPoint}, {StartControlPoint}, {EndControlPoint}, {EndPoint})";
        }
    }
}
