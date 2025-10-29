using System;
using System.Collections.Generic;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
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
            ExecuteEdit(request.Data);
            
            return null;

            #region Internal

            void ExecuteEdit(RailConnectionEditData data)
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
                        fromComponent.ConnectRailComponent(toComponent,data.From.UseFrontSide,data.To.UseFrontSide);
                        break;
                    case RailEditMode.Disconnect:
                        fromComponent.DisconnectRailComponent(toComponent,data.From.UseFrontSide,data.To.UseFrontSide);
                        break;
                }
            }

            RailComponent ResolveComponent(RailCoordinateMessagePack coordinate)
            {
                // ブロック位置から対象ブロックを取得する
                // Obtain the target block from the provided position
                var blockPosition = (Vector3Int)coordinate.Position;
                var block = ServerContext.WorldBlockDatastore.GetBlock(blockPosition);
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
            [Key(2)] public RailConnectionEditData Data { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RailConnectionEditRequest()
            {
            }

            public RailConnectionEditRequest(RailConnectionEditData data)
            {
                Tag = RailConnectionEditProtocol.Tag;
                Data = data;
            }
        }

        [MessagePackObject]
        public class RailConnectionEditData
        {
            [Key(0)] public RailCoordinateMessagePack From { get; set; }
            [Key(1)] public RailCoordinateMessagePack To { get; set; }
            [Key(2)] public RailEditMode Mode { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RailConnectionEditData()
            {
            }

            public RailConnectionEditData(
                RailCoordinateMessagePack from,
                RailCoordinateMessagePack to,
                RailEditMode mode)
            {
                From = from;
                To = to;
                Mode = mode;
            }
        }

        [MessagePackObject]
        public class RailCoordinateMessagePack
        {
            [Key(0)] public Vector3IntMessagePack Position { get; set; }
            [Key(1)] public int ComponentIndex { get; set; }
            [Key(2)] public bool UseFrontSide { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RailCoordinateMessagePack()
            {
            }

            public RailCoordinateMessagePack(Vector3Int position, int componentIndex, bool useFrontSide)
            {
                Position = new Vector3IntMessagePack(position);
                ComponentIndex = componentIndex;
                UseFrontSide = useFrontSide;
            }
        }

        public enum RailEditMode
        {
            Connect = 0,
            Disconnect = 1
        }

        public enum RailConnectionEditError
        {
            None = 0,
            MissingFromComponent = 1,
            MissingToComponent = 2,
            UnsupportedMode = 3
        }
    }
}
