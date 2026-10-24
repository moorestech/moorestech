using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.CleanRoom;
using Game.CleanRoom.Info;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    // ブロックから見た部屋要約・漏れ照会
    // Queries room summaries and leaks seen from a block
    public class GetCleanRoomInfoProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getCleanRoomInfo";

        private readonly CleanRoomInfoQuery _query;

        public GetCleanRoomInfoProtocol(ServiceProvider serviceProvider)
        {
            _query = new CleanRoomInfoQuery(serviceProvider.GetService<CleanRoomDatastore>(), ServerContext.WorldBlockDatastore);
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, int requesterPlayerId)
        {
            var request = MessagePackSerializer.Deserialize<RequestCleanRoomInfoMessagePack>(payload);

            // 照会の間にブロックが壊れていれば BlockNotFound を返し、クライアントのポーラーが警告を出す
            // If the block was removed before the query, return BlockNotFound so the client's poller logs a warning
            var block = ServerContext.WorldBlockDatastore.GetBlock(request.BlockInstanceId);
            if (block == null) return ResponseCleanRoomInfoMessagePack.CreateBlockNotFound();

            return CleanRoomInfoMessagePackFactory.CreateResponse(_query.Query(block));
        }

        #region MessagePack

        [MessagePackObject]
        public class RequestCleanRoomInfoMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public BlockInstanceId BlockInstanceId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestCleanRoomInfoMessagePack() { }

            public RequestCleanRoomInfoMessagePack(BlockInstanceId blockInstanceId)
            {
                Tag = ProtocolTag;
                BlockInstanceId = blockInstanceId;
            }
        }

        [MessagePackObject]
        public class ResponseCleanRoomInfoMessagePack : ProtocolMessagePackBase
        {
            // wire は判別子と種別ごとのキーが並ぶ平らな形。組み立ては下の factory、読み出しは TryReadView だけ
            // The wire is flat with a discriminator and per-kind keys; build only via the factories below and read only via TryReadView
            [Key(2)] public CleanRoomInfoResponseKind Kind { get; set; }
            [Key(3)] public List<CleanRoomSummaryMessagePack> Rooms { get; set; }
            [Key(4)] public CleanRoomLeakMessagePack Leak { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseCleanRoomInfoMessagePack() { }

            private ResponseCleanRoomInfoMessagePack(CleanRoomInfoResponseKind kind)
            {
                Tag = ProtocolTag;
                Kind = kind;
                Rooms = new List<CleanRoomSummaryMessagePack>();
            }

            public static ResponseCleanRoomInfoMessagePack CreateEnclosed(List<CleanRoomSummaryMessagePack> rooms)
            {
                return new ResponseCleanRoomInfoMessagePack(CleanRoomInfoResponseKind.Enclosed) { Rooms = rooms };
            }

            public static ResponseCleanRoomInfoMessagePack CreateNotEnclosed(CleanRoomLeakMessagePack leak)
            {
                return new ResponseCleanRoomInfoMessagePack(CleanRoomInfoResponseKind.NotEnclosed) { Leak = leak };
            }

            public static ResponseCleanRoomInfoMessagePack CreateBlockNotFound()
            {
                return new ResponseCleanRoomInfoMessagePack(CleanRoomInfoResponseKind.BlockNotFound);
            }

            // 判別子の読み分けはここだけで行う。消失は BlockNotFound、欠損や未知の種別は警告して Unreadable
            // The only place that branches on the discriminator; a vanished block is BlockNotFound, broken or unknown kinds warn and are Unreadable
            public BlockInfoReadResult TryReadView(out CleanRoomInfoView view)
            {
                view = default;
                switch (Kind)
                {
                    case CleanRoomInfoResponseKind.Enclosed:
                        // wire から読んだ部屋一覧が空・欠損なら、密閉済みの型を作らず警告して読めないことにする
                        // If the room list read from the wire is empty or broken, build no enclosed view, warn and report it unreadable
                        if (!AreReadableRooms())
                        {
                            Debug.LogWarning($"[GetCleanRoomInfo] Enclosed response has no or broken rooms (count {Rooms?.Count ?? 0}); treating it as unreadable");
                            return BlockInfoReadResult.Unreadable;
                        }

                        view = new CleanRoomEnclosedView(Rooms);
                        return BlockInfoReadResult.Success;
                    case CleanRoomInfoResponseKind.NotEnclosed:
                        if (Leak == null)
                        {
                            Debug.LogWarning("[GetCleanRoomInfo] NotEnclosed response has no leak; treating it as unreadable");
                            return BlockInfoReadResult.Unreadable;
                        }

                        // 漏れの中身の欠損は Leak.TryGetView が警告する
                        // Leak.TryGetView warns about missing leak contents
                        if (!Leak.TryGetView(out var leakView)) return BlockInfoReadResult.Unreadable;
                        view = new CleanRoomNotEnclosedView(leakView);
                        return BlockInfoReadResult.Success;
                    case CleanRoomInfoResponseKind.BlockNotFound:
                        return BlockInfoReadResult.BlockNotFound;
                    default:
                        Debug.LogWarning($"Unreadable response kind: {Kind}");
                        return BlockInfoReadResult.Unreadable;
                }

                #region Internal

                bool AreReadableRooms()
                {
                    if (Rooms == null || Rooms.Count == 0) return false;
                    foreach (var room in Rooms)
                    {
                        if (room == null || room.AirFilters == null) return false;
                        if (!Enum.IsDefined(typeof(CleanRoomClassResponseKind), room.ClassKind))
                        {
                            Debug.LogWarning($"Unreadable room class: {room.ClassKind}");
                            return false;
                        }
                        if (room.ClassKind == CleanRoomClassResponseKind.Classified && room.ClassName == null) return false;
                        foreach (var filter in room.AirFilters)
                        {
                            if (filter == null || filter.Position == null) return false;
                        }
                    }

                    return true;
                }

                #endregion
            }
        }

        #endregion
    }
}
