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
                // 指定された座標からRailComponentを解決する
                // Resolve RailComponents from the specified coordinates
                var fromComponent = ResolveComponent(data.From);
                if (fromComponent == null) return;
                
                var toComponent = ResolveComponent(data.To);
                if (toComponent == null) return;

                // モードに応じて接続または切断を実行する
                // Execute connect or disconnect depending on mode
                switch (data.Mode)
                {
                    case RailEditMode.Connect:
                        fromComponent.ConnectRailComponent(toComponent, true, true);
                        break;
                    case RailEditMode.Disconnect:
                        fromComponent.DisconnectRailComponent(toComponent, true, true);
                        fromComponent.DisconnectRailComponent(toComponent, true, false);
                        fromComponent.DisconnectRailComponent(toComponent, false, true);
                        fromComponent.DisconnectRailComponent(toComponent, false, false);
                        break;
                }
            }

            RailComponent ResolveComponent(Vector3Int position)
            {
                // ブロック位置から対象ブロックを取得する
                // Obtain the target block from the provided position
                var block = ServerContext.WorldBlockDatastore.GetBlock(position);
                if (block == null)
                {
                    return null;
                }
                
                // RailSaverComponentを取得してRailComponentにアクセスする
                // Retrieve the RailSaverComponent to access inner rail components
                return block.TryGetComponent<RailComponent>(out var railComponent) ? railComponent : null;
            }

            #endregion
        }

        [MessagePackObject]
        public class RailConnectionEditRequest : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack From { get; set; }
            [Key(3)] public Vector3IntMessagePack To { get; set; }
            [Key(4)] public RailEditMode Mode { get; set; }
            [Key(5)] public bool ConnectFromIsFront { get; set; }
            [Key(6)] public bool ConnectToIsFront { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RailConnectionEditRequest() { Tag = RailConnectionEditProtocol.Tag; }
            
            public static RailConnectionEditRequest CreateConnectRequest(Vector3Int from, Vector3Int to, bool connectFromIsFront, bool connectToIsFront)
            {
                return new RailConnectionEditRequest
                {
                    From = new Vector3IntMessagePack(from),
                    To = new Vector3IntMessagePack(to),
                    Mode = RailEditMode.Connect,
                    ConnectFromIsFront = connectFromIsFront,
                    ConnectToIsFront = connectToIsFront,
                };
            }

            public static RailConnectionEditRequest CreateDisconnectRequest(Vector3Int from, Vector3Int to){
                return new RailConnectionEditRequest
                {
                    From = new Vector3IntMessagePack(from),
                    To = new Vector3IntMessagePack(to),
                    Mode = RailEditMode.Disconnect,
                };
            }
        }

        public enum RailEditMode
        {
            Connect,
            Disconnect,
        }
    }
}
