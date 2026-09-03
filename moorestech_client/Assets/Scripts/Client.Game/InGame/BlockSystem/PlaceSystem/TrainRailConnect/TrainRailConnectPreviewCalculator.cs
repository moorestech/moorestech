using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
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
            // ノード同士の接続はブロックを設置しないため橋脚コストも予約も無い
            // Connecting two nodes places no block, so there is neither a pier cost nor a reservation
            var length = BezierUtility.GetBezierCurveLength(fromNode, toNode, 64);
            var fromMax = ResolveMaxConnectableRailLength(from, blockGameObjectDataStore);
            var toMax = ResolveMaxConnectableRailLength(to, blockGameObjectDataStore);
            var pierPlacement = EvaluateWithPierReservation(length, fromMax, toMax, playerInventory, connectToolGuid, Array.Empty<ConstructionRequiredItemElement>(), 0);

            // 描画用の制御点を生成
            // Build render control points
            BezierUtility.BuildRenderControlPoints(fromNode.FrontControlPoint, toNode.BackControlPoint, out var p0, out var p1, out var p2, out var p3);
            var isCurvePlaceable = TrainRailCurvePlacementRule.IsPlaceable(p0, p1, p2, p3);
            return new TrainRailConnectPreviewData(p0, p1, p2, p3, pierPlacement.Judgement, isCurvePlaceable, pierPlacement.RailMaterialShortages, pierPlacement.PierMaterialShortages);
        }

        /// <summary>
        /// 終点が新設橋脚の場合。橋脚の建設コストは同一フレームで先に消費されるため予約として判定と不足算出の双方へ渡す
        /// When the endpoint is a newly placed pier; its construction cost is consumed first in the same frame, so it is reserved for both the judgement and the shortage calculation
        /// </summary>
        public static TrainRailConnectPreviewData CalculatePreviewData(ConnectionDestination from, Vector3 placePosition, RailComponentDirection direction, RailGraphClientCache cache, ILocalPlayerInventory playerInventory, BlockGameObjectDataStore blockGameObjectDataStore, float placingBlockMaxConnectableRailLength, Guid connectToolGuid, ConstructionRequiredItemElement[] pierRequiredItems, int pierRequiredCostSets)
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
            var pierPlacement = EvaluateWithPierReservation(length, fromMax, placingBlockMaxConnectableRailLength, playerInventory, connectToolGuid, pierRequiredItems, pierRequiredCostSets);

            var isCurvePlaceable = TrainRailCurvePlacementRule.IsPlaceable(p0, p1, p2, p3);
            return new TrainRailConnectPreviewData(p0, p1, p2, p3, pierPlacement.Judgement, isCurvePlaceable, pierPlacement.RailMaterialShortages, pierPlacement.PierMaterialShortages);
        }

        /// <summary>
        /// 橋脚コストの予約・橋脚自身の不足・レール素材の不足を1つの入口で確定する。予約と不足が同じコストセット数から導かれるため両者はずれない
        /// Settles the pier cost reservation, the pier's own shortage and the rail material shortage at a single entry point; both derive from the same cost-set count so they can never drift apart
        /// 幾何もワールド状態も見ない純関数のため、EditModeテストからそのまま呼べる
        /// A pure function reading neither geometry nor world state, so EditMode tests call it directly
        /// </summary>
        public static TrainRailPierPlacementJudgement EvaluateWithPierReservation(float railLength, float fromMaxConnectableRailLength, float toMaxConnectableRailLength, IEnumerable<IItemStack> inventoryItems, Guid connectToolGuid, ConstructionRequiredItemElement[] pierRequiredItems, int pierRequiredCostSets)
        {
            // 橋脚1セット分の消費素材をコストセット数だけ積み、判定への予約と橋脚自身の不足の双方に使う
            // Stack one pier cost set per required set and use it both as the judgement's reservation and as the pier's own shortage
            var pierItemCounts = new List<(ItemId itemId, int count)>();
            for (var i = 0; i < pierRequiredCostSets; i++) pierItemCounts.AddRange(ConstructionCostItems.ToItemCounts(pierRequiredItems));

            var pierShortages = ConstructionCostShortageCalculator.Calculate(pierRequiredItems, pierRequiredCostSets, inventoryItems);
            var reservedMaterials = ConnectToolMaterialConsumer.ToMaterials(pierItemCounts);
            var judgement = RailConnectionEditProtocol.EvaluatePlacement(railLength, fromMaxConnectableRailLength, toMaxConnectableRailLength, inventoryItems, connectToolGuid, reservedMaterials);

            // 素材不足で落ちたときだけ、判定と同じ長さ・所持・予約から不足素材を算出する（他の理由では行が不要）
            // Only on a material-shortage failure, derive the short materials from the very length, inventory and reservation the judgement used
            var railShortages = judgement.FailureReason == RailConnectionEditProtocol.RailConnectionEditFailureReason.NotEnoughRailItem
                ? (IReadOnlyList<ConstructionMaterialShortage>)ConnectToolMaterialShortageCalculator.Calculate(connectToolGuid, railLength, inventoryItems, reservedMaterials)
                : Array.Empty<ConstructionMaterialShortage>();

            return new TrainRailPierPlacementJudgement(judgement, railShortages, pierShortages);
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
}
