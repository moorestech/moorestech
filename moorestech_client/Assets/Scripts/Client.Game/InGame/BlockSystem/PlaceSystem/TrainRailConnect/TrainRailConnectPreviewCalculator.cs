using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Game.Train.RailCalc;
using Game.Train.SaveLoad;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.ConnectTool;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    /// <summary>
    /// レール橋脚間接続時のプレビュー曲線を計算する
    /// Calculates the rail connection when connecting rail piers to each other
    /// </summary>
    public class TrainRailConnectPreviewCalculator
    {
        /// <summary>
        /// 終点がノードの場合
        /// When the endpoint is a node
        /// </summary>
        public static TrainRailConnectPreviewData CalculatePreviewData(ConnectionDestination from, ConnectionDestination to, RailGraphClientCache cache, ILocalPlayerInventory playerInventory, BlockGameObjectDataStore blockGameObjectDataStore, Guid connectToolGuid)
        {
            // 始点ノードを取得
            // Get the start node
            if (!cache.TryGetNodeId(from, out var fromNodeId) || !cache.TryGetNode(fromNodeId, out var fromNode))
            {
                return TrainRailConnectPreviewData.Invalid;
            }

            // 終点ノードを取得
            // Get the end node
            if (!cache.TryGetNodeId(to, out var toNodeId) || !cache.TryGetNode(toNodeId, out var toNode))
            {
                return TrainRailConnectPreviewData.Invalid;
            }

            // 両端ブロックから最大レール長を解決し、所持中レールアイテムでサーバーと同じ判定を共有する
            // Resolve both endpoints' max length and share the server-side judgement using the held rail item
            var length = BezierUtility.GetBezierCurveLength(fromNode, toNode, 64);
            var fromMax = ResolveMaxConnectableRailLength(from, blockGameObjectDataStore);
            var toMax = ResolveMaxConnectableRailLength(to, blockGameObjectDataStore);
            // ノード同士の接続はブロックを設置しないため予約は無い
            // Connecting two nodes places no block, so there is nothing to reserve
            var judgement = RailConnectionEditProtocol.EvaluatePlacement(length, fromMax, toMax, playerInventory, connectToolGuid, null);
            var materialShortages = ResolveMaterialShortages(judgement, connectToolGuid, length, playerInventory, null);

            // 描画用の制御点を生成
            // Build render control points
            BezierUtility.BuildRenderControlPoints(fromNode.FrontControlPoint, toNode.BackControlPoint, out var p0, out var p1, out var p2, out var p3);
            var isCurvePlaceable = TrainRailCurvePlacementRule.IsPlaceable(p0, p1, p2, p3);
            return new TrainRailConnectPreviewData(p0, p1, p2, p3, judgement, isCurvePlaceable, materialShortages);
        }

        /// <summary>
        /// 終点が新設橋脚の場合。橋脚の建設コストは同一フレームで先に消費されるため予約として判定と不足算出の双方へ渡す
        /// When the endpoint is a newly placed pier; its construction cost is consumed first in the same frame, so it is reserved for both the judgement and the shortage calculation
        /// </summary>
        public static TrainRailConnectPreviewData CalculatePreviewData(ConnectionDestination from, Vector3 placePosition, RailComponentDirection direction, RailGraphClientCache cache, ILocalPlayerInventory playerInventory, BlockGameObjectDataStore blockGameObjectDataStore, float placingBlockMaxConnectableRailLength, Guid connectToolGuid, IReadOnlyList<(ItemId itemId, int count)> pierConstructionItemCounts)
        {
            // 始点ノードを取得
            // Get the start node
            if (!cache.TryGetNodeId(from, out var fromNodeId) || !cache.TryGetNode(fromNodeId, out var fromNode))
            {
                return TrainRailConnectPreviewData.Invalid;
            }

            // 制御点計算に必要な位置と方向を取得
            // Get positions and directions for control points
            var startPosition = fromNode.FrontControlPoint.OriginalPosition;
            var endPosition = placePosition;
            var startDirection = fromNode.FrontControlPoint.ControlPointPosition;
            var endDirection = direction.ToVector3();
            if (endDirection.sqrMagnitude < 1e-6)
            {
                endDirection = new Vector3(0, 1f, 0);
            }
            else
            {
                endDirection.Normalize();
            }
            // 描画用の制御点を生成
            // Build render control points
            BezierUtility.BuildRenderControlPoints(startPosition, endPosition, startDirection, endDirection, out var p0, out var p1, out var p2, out var p3);

            // 始点側ブロックの上限と配置予定ブロックの上限で所持中レールアイテムを使ったサーバーと同じ判定を共有する
            // Share server-side judgement using source block limit, placing block limit, and the held rail item
            var length = BezierUtility.GetBezierCurveLength(p0, p1, p2, p3, 64);
            var fromMax = ResolveMaxConnectableRailLength(from, blockGameObjectDataStore);
            var reservedMaterials = ConnectToolMaterialConsumer.ToMaterials(pierConstructionItemCounts);
            var judgement = RailConnectionEditProtocol.EvaluatePlacement(length, fromMax, placingBlockMaxConnectableRailLength, playerInventory, connectToolGuid, reservedMaterials);
            var materialShortages = ResolveMaterialShortages(judgement, connectToolGuid, length, playerInventory, reservedMaterials);

            var isCurvePlaceable = TrainRailCurvePlacementRule.IsPlaceable(p0, p1, p2, p3);
            return new TrainRailConnectPreviewData(p0, p1, p2, p3, judgement, isCurvePlaceable, materialShortages);
        }

        // 素材不足で落ちたときだけ、判定と同じ長さ・所持・予約から不足素材を算出する（他の理由では行が不要）
        // Only on a material-shortage failure, derive the short materials from the very length, inventory and reservation the judgement used
        private static IReadOnlyList<ConstructionMaterialShortage> ResolveMaterialShortages(RailPlacementJudgement judgement, Guid connectToolGuid, float railLength, ILocalPlayerInventory playerInventory, IReadOnlyList<ConnectToolMaterialCost> reservedMaterials)
        {
            if (judgement.FailureReason != RailConnectionEditProtocol.RailConnectionEditFailureReason.NotEnoughRailItem) return Array.Empty<ConstructionMaterialShortage>();
            return ConnectToolMaterialShortageCalculator.Calculate(connectToolGuid, railLength, playerInventory, reservedMaterials);
        }

        // ConnectionDestination が指すブロックから MaxConnectableRailLength を解決する
        // Resolve MaxConnectableRailLength from the block referenced by ConnectionDestination
        public static float ResolveMaxConnectableRailLength(ConnectionDestination dest, BlockGameObjectDataStore blockGameObjectDataStore)
        {
            if (!blockGameObjectDataStore.TryGetBlockGameObject((Vector3Int)dest.blockPosition, out var blockGameObject)) return float.MaxValue;
            return GetMaxConnectableRailLength(blockGameObject.BlockMasterElement);
        }

        // BlockMasterElement の BlockParam が IRailEndpointBlockParam を実装している場合に値を取り出す
        // Read MaxConnectableRailLength via IRailEndpointBlockParam interface
        public static float GetMaxConnectableRailLength(BlockMasterElement element)
        {
            return element.BlockParam is IRailEndpointBlockParam param ? (float)param.MaxConnectableRailLength : float.MaxValue;
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
            materialShortages: Array.Empty<ConstructionMaterialShortage>());

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

        // 可否は失敗理由とカーブ可否から導出する（「不可なのに理由None」という状態を型で表現不能にする）
        // Placeability is derived from the failure reason and curve placeability (makes "blocked with no reason" unrepresentable)
        public bool IsPlaceable => FailureReason == RailConnectionEditProtocol.RailConnectionEditFailureReason.None && IsCurvePlaceable;

        public TrainRailConnectPreviewData(Vector3 startPoint, Vector3 startControlPoint, Vector3 endControlPoint, Vector3 endPoint, RailPlacementJudgement judgement, bool isClientCurvePlaceable, IReadOnlyList<ConstructionMaterialShortage> materialShortages)
            : this(startPoint, startControlPoint, endControlPoint, endPoint, judgement.SelectedRailTypeGuid, true, judgement.FailureReason, isClientCurvePlaceable, materialShortages)
        {
        }

        private TrainRailConnectPreviewData(Vector3 startPoint, Vector3 startControlPoint, Vector3 endControlPoint, Vector3 endPoint, Guid railTypeGuid, bool isValid, RailConnectionEditProtocol.RailConnectionEditFailureReason failureReason, bool isCurvePlaceable, IReadOnlyList<ConstructionMaterialShortage> materialShortages)
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
        }

        // MaterialShortagesを含めないのは、RailConnectPreviewObjectが毎フレームの同値スキップにこの比較を使うため（新しいリスト参照で毎回不一致になりメッシュを作り直す）
        // MaterialShortages is excluded because RailConnectPreviewObject uses this comparison to skip identical frames (a fresh list reference would mismatch every frame and rebuild the mesh)
        public bool Equals(TrainRailConnectPreviewData other)
        {
            return StartPoint.Equals(other.StartPoint) && StartControlPoint.Equals(other.StartControlPoint) && EndControlPoint.Equals(other.EndControlPoint) && EndPoint.Equals(other.EndPoint) && RailTypeGuid.Equals(other.RailTypeGuid) && IsValid == other.IsValid && FailureReason == other.FailureReason && IsCurvePlaceable == other.IsCurvePlaceable;
        }
        public override bool Equals(object obj)
        {
            return obj is TrainRailConnectPreviewData other && Equals(other);
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(StartPoint, StartControlPoint, EndControlPoint, EndPoint, RailTypeGuid, IsValid, FailureReason, IsCurvePlaceable);
        }
        public override string ToString()
        {
            return $"({StartPoint}, {StartControlPoint}, {EndControlPoint}, {EndPoint})";
        }
    }
}
