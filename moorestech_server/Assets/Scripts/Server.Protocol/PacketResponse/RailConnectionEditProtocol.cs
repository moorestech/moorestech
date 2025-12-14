using System;
using System.Collections.Generic;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Train.RailGraph;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class RailConnectionEditProtocol : IPacketResponse
    {
        public const string Tag = "va:railConnectionEdit";

        private readonly RailConnectionCommandHandler _commandHandler;

        public RailConnectionEditProtocol(ServiceProvider serviceProvider)
        {
            _commandHandler = serviceProvider.GetService<RailConnectionCommandHandler>();
        }

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            // 要求データをデシリアライズする
            // Deserialize request payload
            var request = MessagePackSerializer.Deserialize<RailConnectionEditRequest>(payload.ToArray());

            // 編集処理を実行
            // Execute edit operation
            ExecuteEdit(request);
            
            return null;

            #region Internal

            void ExecuteEdit(RailConnectionEditRequest data)
            {
                if (_commandHandler == null)
                {
                    return;
                }

                // モードに応じて接続または切断を実行する
                // Execute connect or disconnect depending on mode
                switch (data.Mode)
                {
                    case RailEditMode.Connect:
                        _commandHandler.TryConnect(data.FromNodeId, data.FromGuid, data.ToNodeId, data.ToGuid);
                        break;
                    case RailEditMode.Disconnect:
                        _commandHandler.TryDisconnect(data.FromNodeId, data.FromGuid, data.ToNodeId, data.ToGuid);
                        break;
                }
            }

            #endregion
        }

        [MessagePackObject]
        public class RailConnectionEditRequest : ProtocolMessagePackBase
        {
            [Key(2)] public int FromNodeId { get; set; }
            [Key(3)] public Guid FromGuid { get; set; }
            [Key(4)] public int ToNodeId { get; set; }
            [Key(5)] public Guid ToGuid { get; set; }
            [Key(6)] public RailEditMode Mode { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RailConnectionEditRequest() { Tag = RailConnectionEditProtocol.Tag; }

            public static RailConnectionEditRequest CreateConnectRequest(int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid)
            {
                return new RailConnectionEditRequest
                {
                    FromNodeId = fromNodeId,
                    FromGuid = fromGuid,
                    ToNodeId = toNodeId,
                    ToGuid = toGuid,
                    Mode = RailEditMode.Connect,
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
                };
            }
        }

        public enum RailEditMode
        {
            Connect,
            Disconnect,
        }

        /// <summary>
        /// RailComponentを特定するための指定情報
        /// Specification information to identify RailComponent
        /// </summary>
        [MessagePackObject]
        public class RailComponentSpecifier : ProtocolMessagePackBase
        {
            /// <summary>
            /// 指定モード（レールまたは駅）
            /// Specification mode (Rail or Station)
            /// </summary>
            [Key(2)] public RailComponentSpecifierMode Mode { get; set; }

            /// <summary>
            /// ブロックの座標
            /// Block position
            /// </summary>
            [Key(3)] public Vector3IntMessagePack Position { get; set; }

            /// <summary>
            /// 駅モード時のレールインデックス（レールモードでは未使用）
            /// Rail index for Station mode (unused in Rail mode)
            /// </summary>
            [Key(4)] public int RailIndex { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RailComponentSpecifier() { }

            /// <summary>
            /// レールブロックを指定するインスタンスを作成
            /// Create an instance specifying a rail block
            /// </summary>
            public static RailComponentSpecifier CreateRailSpecifier(Vector3Int position)
            {
                return new RailComponentSpecifier
                {
                    Mode = RailComponentSpecifierMode.Rail,
                    Position = new Vector3IntMessagePack(position),
                    RailIndex = 0,
                };
            }

            /// <summary>
            /// 駅ブロックを指定するインスタンスを作成
            /// Create an instance specifying a station block
            /// </summary>
            public static RailComponentSpecifier CreateStationSpecifier(Vector3Int position, int railIndex)
            {
                return new RailComponentSpecifier
                {
                    Mode = RailComponentSpecifierMode.Station,
                    Position = new Vector3IntMessagePack(position),
                    RailIndex = railIndex,
                };
            }
        }

        /// <summary>
        /// RailComponentSpecifierからRailComponentを解決する共通メソッド
        /// Common method to resolve RailComponent from RailComponentSpecifier
        /// </summary>
        public static RailComponent ResolveRailComponent(RailComponentSpecifier specifier)
        {
            if (specifier == null) return null;
            var block = ServerContext.WorldBlockDatastore.GetBlock(specifier.Position.Vector3Int);
            if (block == null) return null;

            switch (specifier.Mode)
            {
                // レールモード：ブロックから直接RailComponentを取得
                // Rail mode: Get RailComponent directly from the block
                case RailComponentSpecifierMode.Rail:
                    return block.TryGetComponent<RailComponent>(out var railComponent) ? railComponent : null;
                
                // 駅モード：RailSaverComponentから配列インデックスで取得
                // Station mode: Get from RailSaverComponent by array index
                case RailComponentSpecifierMode.Station:
                    if (!block.TryGetComponent<RailSaverComponent>(out var railSaverComponent)) return null;

                    var railComponents = railSaverComponent.RailComponents;
                    if (railComponents == null || specifier.RailIndex < 0 || specifier.RailIndex >= railComponents.Length) return null;

                    return railComponents[specifier.RailIndex];

                default:
                    return null;
            }
        }
        
        public enum RailComponentSpecifierMode
        {
            Rail,
            Station,
        }
    }
}
