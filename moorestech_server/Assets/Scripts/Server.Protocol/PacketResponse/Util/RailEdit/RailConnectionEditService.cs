using System;
using Game.PlayerInventory.Interface;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util.ConnectTool;
using static Server.Protocol.PacketResponse.RailConnectionEditProtocol;

namespace Server.Protocol.PacketResponse.Util.RailEdit
{
    internal sealed class RailConnectionEditService
    {
        private readonly RailConnectionCommandHandler _commandHandler;
        private readonly IRailGraphDatastore _railGraphDatastore;
        private readonly TrainRailPositionManager _railPositionManager;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        internal RailConnectionEditService(ServiceProvider serviceProvider)
        {
            _commandHandler = serviceProvider.GetService<RailConnectionCommandHandler>();
            _railGraphDatastore = serviceProvider.GetService<IRailGraphDatastore>();
            _railPositionManager = serviceProvider.GetService<TrainRailPositionManager>();
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }

        internal ResponseRailConnectionEditMessagePack ExecuteEdit(RailConnectionEditRequest data, int requesterPlayerId)
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
                    return HandleConnect(data, requesterPlayerId);
                case RailEditMode.Disconnect:
                    return HandleDisconnect(data, requesterPlayerId);
            }

            return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.InvalidMode, data.Mode);
        }

        private ResponseRailConnectionEditMessagePack HandleConnect(RailConnectionEditRequest data, int requesterPlayerId)
        {
            if (!_commandHandler.TryResolveNodes(data.FromNodeId, data.FromGuid, data.ToNodeId, data.ToGuid, out var fromNode, out var toNode))
                return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.InvalidNode, data.Mode);

            // 未解放または未指定(Empty)のconnectToolによる接続要求は拒否する（電線・歯車の4経路と対称）
            // Reject connection requests with an unlocked or unspecified (Empty) connectTool, symmetric with the electric-wire/gear-chain paths
            if (!ConnectToolSelector.IsUnlocked(data.ConnectToolGuid))
                return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.NotUnlocked, data.Mode);

            var length = GetRailLength(fromNode, toNode);
            var inventory = _playerInventoryDataStore.GetInventoryData(requesterPlayerId).MainOpenableInventory;

            // 長さ・両端ブロック上限・所持インベントリ・選択connectToolから設置可否と消費素材を一括確定
            // Single placement evaluation shared with the client preview
            // 既設ノード同士の接続はブロックを設置しないため予約は無い
            // Connecting two existing nodes places no block, so there is nothing to reserve
            var judgement = EvaluatePlacement(length, fromNode.MaxConnectableRailLength, toNode.MaxConnectableRailLength, inventory.InventoryItems, data.ConnectToolGuid, null);
            if (!judgement.IsPlaceable)
                return ResponseRailConnectionEditMessagePack.CreateFailure(judgement.FailureReason, data.Mode);

            // RailTypeGuidにconnectToolGuidを格納
            // Store connectToolGuid into the RailGraph RailTypeGuid slot
            var connectResult = _commandHandler.TryConnect(data.FromNodeId, data.FromGuid, data.ToNodeId, data.ToGuid, data.ConnectToolGuid);
            if (connectResult)
            {
                ConnectToolMaterialConsumer.Consume(judgement.Materials, inventory);
            }

            return ResponseRailConnectionEditMessagePack.Create(connectResult, connectResult ? RailConnectionEditFailureReason.None : RailConnectionEditFailureReason.InvalidNode, data.Mode);
        }

        private ResponseRailConnectionEditMessagePack HandleDisconnect(RailConnectionEditRequest data, int requesterPlayerId)
        {
            if (!_railGraphDatastore.TryGetRailNode(data.FromNodeId, out var fromNode) || fromNode == null || fromNode.Guid != data.FromGuid)
            {
                return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.InvalidNode, data.Mode);
            }

            if (!_railGraphDatastore.TryGetRailNode(data.ToNodeId, out var toNode) || toNode == null || toNode.Guid != data.ToGuid)
            {
                return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.InvalidNode, data.Mode);
            }

            if (fromNode.StationRef.IsSameStation(toNode.StationRef))
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

            // 区間の返却素材を算出する。無償・算出不能（ログ済み）は返却なしで切断する
            // Compute the segment refund; costless or uncomputable (already logged) segments disconnect without refund
            var inventory = _playerInventoryDataStore.GetInventoryData(requesterPlayerId).MainOpenableInventory;
            if (!RailRemovalRefundCalculator.TryCalculateSegmentRefundMaterials(_railGraphDatastore, fromNode, toNode, out var materials))
            {
                var disconnected = _commandHandler.TryDisconnect(data.FromNodeId, data.FromGuid, data.ToNodeId, data.ToGuid);
                return ResponseRailConnectionEditMessagePack.Create(disconnected, disconnected ? RailConnectionEditFailureReason.None : RailConnectionEditFailureReason.UnknownError, data.Mode);
            }

            // インベントリ満杯時は削除不可
            // Abort when there is no inventory space
            if (!ConnectToolMaterialConsumer.TryCreateFittingRefund(materials, inventory, out var refundStacks))
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
    }
}
