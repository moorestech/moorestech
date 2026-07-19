using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.PlayerInventory.Interface;
using Game.Train.RailCalc;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util.ConnectTool;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class RailConnectionEditProtocol : IPacketResponse
    {
        public const string Tag = "va:railConnectionEdit";

        private readonly RailConnectionCommandHandler _commandHandler;
        private readonly IRailGraphDatastore _railGraphDatastore;
        private readonly TrainRailPositionManager _railPositionManager;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public RailConnectionEditProtocol(ServiceProvider serviceProvider)
        {
            _commandHandler = serviceProvider.GetService<RailConnectionCommandHandler>();
            _railGraphDatastore = serviceProvider.GetService<IRailGraphDatastore>();
            _railPositionManager = serviceProvider.GetService<TrainRailPositionManager>();
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            // 要求データをデシリアライズする
            // Deserialize request payload
            var request = MessagePackSerializer.Deserialize<RailConnectionEditRequest>(payload);

            // 編集処理を実行
            // Execute edit operation
            return ExecuteEdit(request);

            #region Internal

            ResponseRailConnectionEditMessagePack ExecuteEdit(RailConnectionEditRequest data)
            {
                if (_commandHandler == null)
                {
                    return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.UnknownError, data.Mode);
                }

                // モードに応じて接続または切断を実行する
                // Execute connect or disconnect depending on mode
                switch (data.Mode)
                {
                    case RailEditMode.Connect:
                        return HandleConnect(data);
                    case RailEditMode.Disconnect:
                        return HandleDisconnect(data);
                }

                return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.InvalidMode, data.Mode);
            }

            ResponseRailConnectionEditMessagePack HandleConnect(RailConnectionEditRequest data)
            {
                if (!_commandHandler.TryResolveNodes(data.FromNodeId, data.FromGuid, data.ToNodeId, data.ToGuid, out var fromNode, out var toNode))
                    return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.InvalidNode, data.Mode);

                // 未解放または未指定(Empty)のconnectToolによる接続要求は拒否する（電線・歯車の4経路と対称）
                // Reject connection requests with an unlocked or unspecified (Empty) connectTool, symmetric with the electric-wire/gear-chain paths
                if (!ConnectToolSelector.IsUnlocked(data.ConnectToolGuid))
                    return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.NotUnlocked, data.Mode);

                var length = GetRailLength(fromNode, toNode);
                var inventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;

                // 長さ・両端ブロック上限・所持インベントリ・選択connectToolから設置可否と消費素材を一括確定
                // Single placement evaluation shared with the client preview
                var judgement = EvaluatePlacement(length, fromNode.MaxConnectableRailLength, toNode.MaxConnectableRailLength, inventory.InventoryItems, data.ConnectToolGuid);
                if (!judgement.IsPlaceable)
                    return ResponseRailConnectionEditMessagePack.CreateFailure(judgement.FailureReason, data.Mode);

                // 成功したらインベントリから引く。RailGraphのRailTypeGuidにはconnectToolGuidを格納する
                // Consume materials on success; store the connectToolGuid into the RailGraph RailTypeGuid slot
                var connectResult = _commandHandler.TryConnect(data.FromNodeId, data.FromGuid, data.ToNodeId, data.ToGuid, data.ConnectToolGuid);
                if (connectResult)
                {
                    ConnectToolMaterialConsumer.Consume(judgement.Materials, inventory);
                }

                return ResponseRailConnectionEditMessagePack.Create(connectResult, connectResult ? RailConnectionEditFailureReason.None : RailConnectionEditFailureReason.InvalidNode, data.Mode);
            }

            ResponseRailConnectionEditMessagePack HandleDisconnect(RailConnectionEditRequest data)
            {
                if (!_railGraphDatastore.TryGetRailNode(data.FromNodeId, out var fromNode) || fromNode == null || fromNode.Guid != data.FromGuid)
                {
                    return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.InvalidNode, data.Mode);
                }

                if (!_railGraphDatastore.TryGetRailNode(data.ToNodeId, out var toNode) || toNode == null || toNode.Guid != data.ToGuid)
                {
                    return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.InvalidNode, data.Mode);
                }

                if (IsStationInternalEdge(fromNode, toNode))
                {
                    return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.StationInternalEdge, data.Mode);
                }

                if (!_railPositionManager.CanRemoveEdge(fromNode, toNode))
                {
                    return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.NodeInUseByTrain, data.Mode);
                }
                if (!_railPositionManager.CanRemoveEdge(toNode.OppositeRailNode, fromNode.OppositeRailNode))
                {
                    return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.NodeInUseByTrain, data.Mode);
                }

                // セグメントに格納されたconnectToolGuidと距離から返却素材を算出する
                // Compute refund materials from the segment's stored connectToolGuid and length
                var connectToolGuid = ResolveConnectToolGuid(data.FromNodeId, data.ToNodeId);
                var inventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;
                var railLength = GetRailLength(fromNode, toNode);

                // 無コスト接続や算出不能な場合は返却なしで切断する
                // Disconnect without refund for costless connections or when the cost cannot be computed
                if (connectToolGuid == Guid.Empty || !ConnectToolCostCalculator.TryCalculate(connectToolGuid, railLength, out var materials))
                {
                    var disconnected = _commandHandler.TryDisconnect(data.FromNodeId, data.FromGuid, data.ToNodeId, data.ToGuid);
                    return ResponseRailConnectionEditMessagePack.Create(disconnected, disconnected ? RailConnectionEditFailureReason.None : RailConnectionEditFailureReason.UnknownError, data.Mode);
                }

                var refundStacks = ConnectToolMaterialConsumer.CreateRefundItems(materials);

                // playerインベントリに空きがない場合は削除不可
                // Abort when there is no inventory space to return the items
                if (0 < refundStacks.Count && !inventory.InsertionCheck(refundStacks))
                {
                    return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.NotEnoughInventorySpace, data.Mode);
                }

                var disconnectedflag = _commandHandler.TryDisconnect(data.FromNodeId, data.FromGuid, data.ToNodeId, data.ToGuid);

                // アイテムを返却
                // Return rail materials
                if (disconnectedflag)
                {
                    foreach (var refundStack in refundStacks) inventory.InsertItem(refundStack);
                }

                return ResponseRailConnectionEditMessagePack.Create(disconnectedflag, disconnectedflag ? RailConnectionEditFailureReason.None : RailConnectionEditFailureReason.UnknownError, data.Mode);
            }

            bool IsStationInternalEdge(RailNode from, RailNode to)
            {
                if (!from.StationRef.HasStation || !to.StationRef.HasStation)
                {
                    return false;
                }
                return from.StationRef.StationBlockInstanceId.Equals(to.StationRef.StationBlockInstanceId);
            }

            // レール種別（connectToolGuid）をセグメントから解決する
            // Resolve rail type (connectToolGuid) from the segment data
            Guid ResolveConnectToolGuid(int fromNodeId, int toNodeId)
            {
                return _railGraphDatastore.TryGetRailSegmentType(fromNodeId, toNodeId, out var connectToolGuid) ? connectToolGuid : Guid.Empty;
            }

            #endregion
        }

        /// <summary>
        /// 接続区間の長さ・両端ブロックの最大上限・所持インベントリ・選択connectToolから設置可否と消費素材を一括で確定する。
        /// サーバー・クライアント双方からこのメソッドだけを呼ぶことで、設置条件の追加がここに集約される。
        /// connectToolGuid が Guid.Empty のときは無コスト接続として扱う。
        /// Single entry point for placement viability, shared by server and client.
        /// When connectToolGuid is Guid.Empty the connection is treated as costless.
        /// </summary>
        public static RailPlacementJudgement EvaluatePlacement(float railLength, float fromMaxConnectableRailLength, float toMaxConnectableRailLength, IEnumerable<IItemStack> inventoryItems, Guid connectToolGuid)
        {
            // 両端の上限の min をその接続区間の許容最大長とする
            // Take the smaller endpoint limit as the allowed maximum for the segment
            if (Mathf.Min(fromMaxConnectableRailLength, toMaxConnectableRailLength) < railLength)
                return new RailPlacementJudgement(RailConnectionEditFailureReason.RailLengthExceeded, connectToolGuid, null);

            // 無コスト接続は素材不要
            // Costless connection needs no materials
            if (connectToolGuid == Guid.Empty)
                return new RailPlacementJudgement(RailConnectionEditFailureReason.None, connectToolGuid, Array.Empty<ConnectToolMaterialCost>());

            // connectToolマスタから複数素材の必要数を算出し、所持を確認する
            // Compute the multi-material requirement from the connectTool master and verify ownership
            if (!ConnectToolCostCalculator.TryCalculate(connectToolGuid, railLength, out var materials))
                return new RailPlacementJudgement(RailConnectionEditFailureReason.NotEnoughRailItem, connectToolGuid, null);

            var items = inventoryItems as IReadOnlyList<IItemStack> ?? inventoryItems.ToList();
            if (!ConnectToolMaterialConsumer.HasEnough(materials, items))
                return new RailPlacementJudgement(RailConnectionEditFailureReason.NotEnoughRailItem, connectToolGuid, null);

            return new RailPlacementJudgement(RailConnectionEditFailureReason.None, connectToolGuid, materials);
        }

        public static float GetRailLength(IRailNode fromNode, IRailNode toNode)
        {
            var p0 = fromNode.FrontControlPoint.OriginalPosition;
            var p1 = fromNode.FrontControlPoint.OriginalPosition + fromNode.FrontControlPoint.ControlPointPosition;
            var p2 = toNode.BackControlPoint.OriginalPosition + toNode.BackControlPoint.ControlPointPosition;
            var p3 = toNode.BackControlPoint.OriginalPosition;
            var length = BezierUtility.GetBezierCurveLength(p0, p1, p2, p3, 64);
            return length;
        }

        [MessagePackObject]
        public class RailConnectionEditRequest : ProtocolMessagePackBase
        {
            [Key(2)] public int FromNodeId { get; set; }
            [Key(3)] public Guid FromGuid { get; set; }
            [Key(4)] public int ToNodeId { get; set; }
            [Key(5)] public Guid ToGuid { get; set; }
            [Key(6)] public RailEditMode Mode { get; set; }
            [Key(7)] public int PlayerId { get; set; }
            [Key(8)] public Guid ConnectToolGuid { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RailConnectionEditRequest() { Tag = RailConnectionEditProtocol.Tag; }

            public static RailConnectionEditRequest CreateConnectRequest(int playerId, int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid, Guid connectToolGuid)
            {
                return new RailConnectionEditRequest
                {
                    PlayerId = playerId,
                    FromNodeId = fromNodeId,
                    FromGuid = fromGuid,
                    ToNodeId = toNodeId,
                    ToGuid = toGuid,
                    Mode = RailEditMode.Connect,
                    ConnectToolGuid = connectToolGuid,
                };
            }

            public static RailConnectionEditRequest CreateDisconnectRequest(int playerId, int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid)
            {
                return new RailConnectionEditRequest
                {
                    PlayerId = playerId,
                    FromNodeId = fromNodeId,
                    FromGuid = fromGuid,
                    ToNodeId = toNodeId,
                    ToGuid = toGuid,
                    Mode = RailEditMode.Disconnect,
                    ConnectToolGuid = Guid.Empty,
                };
            }
        }

        [MessagePackObject]
        public class ResponseRailConnectionEditMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public bool Success { get; set; }
            [Key(3)] public RailConnectionEditFailureReason FailureReason { get; set; }
            [Key(4)] public RailEditMode Mode { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseRailConnectionEditMessagePack()
            {
                Tag = RailConnectionEditProtocol.Tag;
            }

            public static ResponseRailConnectionEditMessagePack Create(bool success, RailConnectionEditFailureReason reason, RailEditMode mode)
            {
                return new ResponseRailConnectionEditMessagePack
                {
                    Success = success,
                    FailureReason = reason,
                    Mode = mode,
                };
            }

            public static ResponseRailConnectionEditMessagePack CreateFailure(RailConnectionEditFailureReason reason, RailEditMode mode)
            {
                return Create(false, reason, mode);
            }
        }

        public enum RailEditMode
        {
            Connect,
            Disconnect,
        }

        public enum RailConnectionEditFailureReason
        {
            None,
            InvalidNode,
            NodeInUseByTrain,
            StationInternalEdge,
            InvalidMode,
            NotEnoughRailItem,
            NotEnoughInventorySpace,
            RailLengthExceeded,
            NotUnlocked,
            UnknownError,
        }
    }

    /// <summary>
    /// レール設置可否の統合判定結果。失敗理由、または採用connectToolと消費素材を保持する
    /// Aggregated rail placement viability result, exposing the failure reason or the selected connectTool with its consumption materials.
    /// </summary>
    public readonly struct RailPlacementJudgement
    {
        public readonly RailConnectionEditProtocol.RailConnectionEditFailureReason FailureReason;
        public readonly Guid ConnectToolGuid;
        public readonly IReadOnlyList<ConnectToolMaterialCost> Materials;

        public bool IsPlaceable => FailureReason == RailConnectionEditProtocol.RailConnectionEditFailureReason.None;

        public Guid SelectedRailTypeGuid => ConnectToolGuid;

        public RailPlacementJudgement(RailConnectionEditProtocol.RailConnectionEditFailureReason failureReason, Guid connectToolGuid, IReadOnlyList<ConnectToolMaterialCost> materials)
        {
            FailureReason = failureReason;
            ConnectToolGuid = connectToolGuid;
            Materials = materials;
        }
    }
}
