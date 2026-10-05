using System;
using System.Linq;
using Game.Train.RailGraph;
using Game.Train.SaveLoad;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.Notification;
using Server.Protocol.PacketResponse.Util.RailEdit;
using Server.Util.MessagePack;
using UnityEngine;
using static Server.Protocol.PacketResponse.RailConnectionEditProtocol;

namespace Server.Protocol.PacketResponse.Rail
{
    /// <summary>
    ///     ブロック座標で同定したレール端点同士を接続する。再設置でノードId/Guidが変わるUndo復元が使う。応答は返さない
    ///     Connects rail endpoints identified by block position; used by undo restore, where re-placement changes node ids and guids. Sends no response
    /// </summary>
    public class RailConnectByDestinationProtocol : IPacketResponse
    {
        public const string Tag = "va:railConnectByDestination";

        private readonly RailConnectionEditService _editService;
        private readonly IRailGraphDatastore _railGraphDatastore;
        private readonly NotificationService _notificationService;

        public RailConnectByDestinationProtocol(ServiceProvider serviceProvider)
        {
            _editService = new RailConnectionEditService(serviceProvider);
            _railGraphDatastore = serviceProvider.GetService<IRailGraphDatastore>();
            _notificationService = serviceProvider.GetService<NotificationService>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, int requesterPlayerId)
        {
            var request = MessagePackSerializer.Deserialize<RailConnectByDestinationRequest>(payload);
            Connect(request, requesterPlayerId);
            return null;

            #region Internal

            void Connect(RailConnectByDestinationRequest data, int playerId)
            {
                // 外部入力に端点情報が無ければ解決前に拒否する
                // Reject missing endpoint data at the external input boundary
                if (data.From?.BlockPosition == null || data.To?.BlockPosition == null)
                {
                    NotifyDenied(RailConnectionEditFailureReason.InvalidNode);
                    return;
                }

                // 座標から現在のノードを解決する（再設置後はId・Guidが撤去前と違う）
                // Resolve the current nodes from positions (ids and guids differ from before removal)
                var fromNode = _railGraphDatastore.ResolveRailNode(data.From.ToModel());
                var toNode = _railGraphDatastore.ResolveRailNode(data.To.ToModel());
                if (fromNode == null || toNode == null)
                {
                    Debug.LogWarning($"[RailConnectByDestination] endpoint not found. from={data.From.BlockPosition.Vector3Int}/{data.From.ComponentIndex}/{data.From.IsFrontSide} to={data.To.BlockPosition.Vector3Int}/{data.To.ComponentIndex}/{data.To.IsFrontSide}");
                    NotifyDenied(RailConnectionEditFailureReason.InvalidNode);
                    return;
                }

                // 既に繋がっていれば二重課金せず何もしない（駅の隣接自動接続などで先に復元済み）
                // Already connected: do nothing and charge nothing (e.g. already restored by station adjacency)
                if (_railGraphDatastore.GetConnectedNodesWithDistance(fromNode).Any(connected => connected.Item1.NodeId == toNode.NodeId))
                {
                    Debug.Log($"[RailConnectByDestination] already connected, skipped. from={fromNode.NodeId} to={toNode.NodeId}");
                    return;
                }

                // 解決したId/Guidで既存の接続処理（解放・長さ・素材の判定と消費）をそのまま通し、失敗理由だけ通知へ回す
                // Run the existing connect path (unlock, length, material check and consumption) with the resolved id/guid; only forward a failure reason
                var editRequest = RailConnectionEditRequest.CreateConnectRequest(fromNode.NodeId, fromNode.NodeGuid, toNode.NodeId, toNode.NodeGuid, data.ConnectToolGuid);
                var result = _editService.ExecuteEdit(editRequest, playerId);
                if (!result.Success) NotifyDenied(result.FailureReason);
            }

            // 応答を返さないため、拒否理由は通知でプレイヤーへ届ける
            // No response is sent, so deliver the denial reason to the player as a notification
            void NotifyDenied(RailConnectionEditFailureReason reason)
            {
                Debug.LogWarning($"[RailConnectByDestination] connection denied: {reason}");
                _notificationService.Notify(requesterPlayerId, NotificationMessagePack.CreateOperationDenied($"denied.railEdit.{reason}", Array.Empty<string>()));
            }

            #endregion
        }

        [MessagePackObject]
        public class RailConnectByDestinationRequest : ProtocolMessagePackBase
        {
            [Key(2)] public ConnectionDestinationMessagePack From;
            [Key(3)] public ConnectionDestinationMessagePack To;
            [Key(4)] public Guid ConnectToolGuid;

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RailConnectByDestinationRequest() { Tag = RailConnectByDestinationProtocol.Tag; }

            public RailConnectByDestinationRequest(ConnectionDestination from, ConnectionDestination to, Guid connectToolGuid)
            {
                Tag = RailConnectByDestinationProtocol.Tag;
                From = new ConnectionDestinationMessagePack(from);
                To = new ConnectionDestinationMessagePack(to);
                ConnectToolGuid = connectToolGuid;
            }
        }
    }
}
