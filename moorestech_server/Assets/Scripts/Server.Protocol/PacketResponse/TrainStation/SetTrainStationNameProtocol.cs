using System;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface.Extension;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class SetTrainStationNameProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:setTrainStationName";

        public SetTrainStationNameProtocol(ServiceProvider serviceProvider)
        {
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<SetTrainStationNameRequest>(payload);
            return Apply(request);

            #region Internal

            ProtocolMessagePackBase Apply(SetTrainStationNameRequest data)
            {
                // 外部入力の欠損と空白を検証する
                // Validate missing input and whitespace at the protocol boundary
                if (data.Position == null) return Reject(data, SetTrainStationNameFailureReason.InvalidRequest);
                var name = (data.StationName ?? string.Empty).Trim();
                if (name.Length == 0) return Reject(data, SetTrainStationNameFailureReason.EmptyName);

                // 駅コンポーネントを持つブロックだけを改名する
                // Rename only blocks with a station component
                var block = ServerContext.WorldBlockDatastore.GetBlock(data.Position.Vector3Int);
                if (block == null) return Reject(data, SetTrainStationNameFailureReason.BlockNotFound);
                if (!block.TryGetComponent<TrainStationComponent>(out var station)) return Reject(data, SetTrainStationNameFailureReason.NotTrainStation);

                // ブロック状態の既存通知経路へ変更を流す
                // Publish through the existing block state notification path
                station.SetStationName(name);
                return new SetTrainStationNameResponse(true, name, SetTrainStationNameFailureReason.None);
            }

            ProtocolMessagePackBase Reject(SetTrainStationNameRequest data, SetTrainStationNameFailureReason reason)
            {
                // 拒否理由をログへ残す（無音の縮退禁止）
                // Log the rejection reason (no silent fallback)
                Debug.LogWarning($"[SetTrainStationName] rejected reason={reason} pos={data.Position?.ToString() ?? "missing"}");
                return new SetTrainStationNameResponse(false, string.Empty, reason);
            }

            #endregion
        }

        public enum SetTrainStationNameFailureReason
        {
            None,
            BlockNotFound,
            NotTrainStation,
            EmptyName,
            InvalidRequest,
        }

        #region MessagePack

        [MessagePackObject]
        public class SetTrainStationNameRequest : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack Position;
            [Key(3)] public string StationName;

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public SetTrainStationNameRequest() { }

            public SetTrainStationNameRequest(Vector3Int position, string stationName)
            {
                Tag = ProtocolTag;
                Position = new Vector3IntMessagePack(position);
                StationName = stationName;
            }
        }

        [MessagePackObject]
        public class SetTrainStationNameResponse : ProtocolMessagePackBase
        {
            [Key(2)] public bool Success;
            [Key(3)] public string AppliedName;
            [Key(4)] public SetTrainStationNameFailureReason FailureReason;

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public SetTrainStationNameResponse() { }

            public SetTrainStationNameResponse(bool success, string appliedName, SetTrainStationNameFailureReason failureReason)
            {
                Tag = ProtocolTag;
                Success = success;
                AppliedName = appliedName;
                FailureReason = failureReason;
            }
        }

        #endregion
    }
}
