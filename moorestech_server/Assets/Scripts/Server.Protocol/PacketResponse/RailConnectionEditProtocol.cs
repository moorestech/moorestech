using Server.Protocol.PacketResponse.Util.RailEdit;
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
using Game.Train.RailCalc;
using Game.Train.RailGraph;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.Notification;
using Server.Protocol.PacketResponse.Util.ConnectTool;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class RailConnectionEditProtocol : IPacketResponse
    {
        public const string Tag = "va:railConnectionEdit";

        private readonly RailConnectionEditService _editService;
        private readonly NotificationService _notificationService;

        public RailConnectionEditProtocol(ServiceProvider serviceProvider)
        {
            _editService = new RailConnectionEditService(serviceProvider);
            _notificationService = serviceProvider.GetService<NotificationService>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, int requesterPlayerId)
        {
            // 要求データをデシリアライズする
            // Deserialize request payload
            var request = MessagePackSerializer.Deserialize<RailConnectionEditRequest>(payload);

            // 編集処理を実行
            // Execute edit operation
            var response = _editService.ExecuteEdit(request, requesterPlayerId);

            // 失敗応答はSendOnlyで破棄されるため、通知基盤経由でプレイヤーに理由を届ける
            // Failure responses are discarded by SendOnly, so deliver the reason via the notification service
            if (!response.Success)
            {
                _notificationService.Notify(requesterPlayerId, NotificationMessagePack.CreateOperationDenied($"denied.railEdit.{response.FailureReason}", Array.Empty<string>()));
            }

            return response;

        }

        /// <summary>
        /// 接続区間の長さ・両端ブロックの最大上限・所持インベントリ・選択connectToolから設置可否と消費素材を一括で確定する。
        /// サーバー・クライアント双方からこのメソッドだけを呼ぶことで、設置条件の追加がここに集約される。
        /// connectToolGuid が Guid.Empty のときは無コスト接続として扱う。
        /// reservedMaterials は同一フレームで設置する橋脚の建設コスト等、レール素材より先に押さえられる分を必要数へ上乗せする。
        /// Single entry point for placement viability, shared by server and client.
        /// When connectToolGuid is Guid.Empty the connection is treated as costless.
        /// reservedMaterials adds amounts claimed ahead of the rail cost (e.g. a pier placed in the same frame) on top of the requirement.
        /// </summary>
        public static RailPlacementJudgement EvaluatePlacement(float railLength, float fromMaxConnectableRailLength, float toMaxConnectableRailLength, IEnumerable<IItemStack> inventoryItems, Guid connectToolGuid, IReadOnlyList<ConnectToolMaterialCost> reservedMaterials)
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
            if (!ConstructionMaterialAccounting.HasEnough(materials, items, reservedMaterials))
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
            [Key(8)] public Guid ConnectToolGuid { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RailConnectionEditRequest() { Tag = RailConnectionEditProtocol.Tag; }

            public static RailConnectionEditRequest CreateConnectRequest(int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid, Guid connectToolGuid)
            {
                return new RailConnectionEditRequest
                {
                    FromNodeId = fromNodeId,
                    FromGuid = fromGuid,
                    ToNodeId = toNodeId,
                    ToGuid = toGuid,
                    Mode = RailEditMode.Connect,
                    ConnectToolGuid = connectToolGuid,
                };
            }

            public static RailConnectionEditRequest CreateDisconnectRequest(int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid)
            {
                return new RailConnectionEditRequest
                {
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

}
