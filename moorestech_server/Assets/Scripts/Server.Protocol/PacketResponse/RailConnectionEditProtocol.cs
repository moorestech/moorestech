using System;
using System.Collections.Generic;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface.Extension;
using Game.Context;
using MessagePack;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class RailConnectionEditProtocol : IPacketResponse
    {
        public const string Tag = "va:railConnectionEdit";

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            // 要求データをデシリアライズする
            // Deserialize request payload
            var request = MessagePackSerializer.Deserialize<RailConnectionEditRequest>(payload.ToArray());

            // 編集処理を実行し、結果データを構築する
            // Execute edit operation and build response data
            ExecuteEdit(request);
            
            return null;

            #region Internal

            void ExecuteEdit(RailConnectionEditRequest data)
            {
                // 指定された情報からRailComponentを解決する
                // Resolve RailComponents from the specified information
                var fromComponent = ResolveComponent(data.From);
                if (fromComponent == null) return;

                var toComponent = ResolveComponent(data.To);
                if (toComponent == null) return;

                // モードに応じて接続または切断を実行する
                // Execute connect or disconnect depending on mode
                switch (data.Mode)
                {
                    case RailEditMode.Connect:
                        fromComponent.ConnectRailComponent(toComponent, data.ConnectFromIsFront, data.ConnectToIsFront);
                        break;
                    case RailEditMode.Disconnect:
                        fromComponent.DisconnectRailComponent(toComponent, true, true);
                        fromComponent.DisconnectRailComponent(toComponent, true, false);
                        fromComponent.DisconnectRailComponent(toComponent, false, true);
                        fromComponent.DisconnectRailComponent(toComponent, false, false);
                        break;
                }
            }

            RailComponent ResolveComponent(RailComponentSpecifier specifier)
            {
                // ブロック位置から対象ブロックを取得する
                // Obtain the target block from the provided position
                var block = ServerContext.WorldBlockDatastore.GetBlock(specifier.Position.Vector3Int);
                if (block == null)
                {
                    return null;
                }

                // モードに応じてRailComponentを解決する
                // Resolve RailComponent based on the mode
                switch (specifier.Mode)
                {
                    case RailComponentSpecifierMode.Rail:
                        // レールモード：ブロックから直接RailComponentを取得
                        // Rail mode: Get RailComponent directly from the block
                        return block.TryGetComponent<RailComponent>(out var railComponent) ? railComponent : null;

                    case RailComponentSpecifierMode.Station:
                        // 駅モード：RailSaverComponentから配列インデックスで取得
                        // Station mode: Get from RailSaverComponent by array index
                        if (!block.TryGetComponent<RailSaverComponent>(out var railSaverComponent)) return null;

                        var railComponents = railSaverComponent.RailComponents;
                        if (railComponents == null || specifier.RailIndex < 0 || specifier.RailIndex >= railComponents.Length) return null;

                        return railComponents[specifier.RailIndex];

                    default:
                        return null;
                }
            }

            #endregion
        }

        [MessagePackObject]
        public class RailConnectionEditRequest : ProtocolMessagePackBase
        {
            [Key(2)] public RailComponentSpecifier From { get; set; }
            [Key(3)] public RailComponentSpecifier To { get; set; }
            [Key(4)] public RailEditMode Mode { get; set; }
            [Key(5)] public bool ConnectFromIsFront { get; set; }
            [Key(6)] public bool ConnectToIsFront { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RailConnectionEditRequest() { Tag = RailConnectionEditProtocol.Tag; }

            public static RailConnectionEditRequest CreateConnectRequest(RailComponentSpecifier from, RailComponentSpecifier to, bool connectFromIsFront, bool connectToIsFront)
            {
                return new RailConnectionEditRequest
                {
                    From = from,
                    To = to,
                    Mode = RailEditMode.Connect,
                    ConnectFromIsFront = connectFromIsFront,
                    ConnectToIsFront = connectToIsFront,
                };
            }

            public static RailConnectionEditRequest CreateDisconnectRequest(RailComponentSpecifier from, RailComponentSpecifier to)
            {
                return new RailConnectionEditRequest
                {
                    From = from,
                    To = to,
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
        /// RailComponentの指定モード
        /// Mode for specifying RailComponent
        /// </summary>
        public enum RailComponentSpecifierMode
        {
            /// <summary>
            /// レールブロックを指定（座標のみ）
            /// Specify rail block (position only)
            /// </summary>
            Rail,
            
            /// <summary>
            /// 駅ブロックを指定（座標とレールインデックス）
            /// Specify station block (position and rail index)
            /// </summary>
            Station,
        }
    }
}
