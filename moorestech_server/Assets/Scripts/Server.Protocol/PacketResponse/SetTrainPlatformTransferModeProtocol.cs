using System;
using Game.Block.Blocks.TrainRail;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class SetTrainPlatformTransferModeProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:setTrainPlatformTransferMode";

        public SetTrainPlatformTransferModeProtocol(ServiceProvider serviceProvider)
        {
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<SetTrainPlatformTransferModeRequest>(payload);
            return ApplyMode(request);

            #region Internal

            ProtocolMessagePackBase ApplyMode(SetTrainPlatformTransferModeRequest data)
            {
                // 指定座標のブロックを取得
                // Fetch the block at the requested position
                var block = ServerContext.WorldBlockDatastore.GetBlock(data.Position.Vector3Int);
                if (block == null)
                {
                    return new SetTrainPlatformTransferModeResponse(false, data.Mode, SetTrainPlatformTransferModeFailureReason.BlockNotFound);
                }

                // 列車プラットフォームのモードコンポーネントを取り出す
                // Try to fetch the train platform's transfer-mode component
                if (!block.ComponentManager.TryGetComponent<TrainPlatformTransferComponent>(out var transfer))
                {
                    return new SetTrainPlatformTransferModeResponse(false, data.Mode, SetTrainPlatformTransferModeFailureReason.NotTrainPlatform);
                }

                // ロード/アンロードモードを切り替える
                // Switch the load/unload transfer mode
                transfer.SetMode(data.Mode);
                return new SetTrainPlatformTransferModeResponse(true, transfer.Mode, SetTrainPlatformTransferModeFailureReason.None);
            }

            #endregion
        }

        #region MessagePack

        [MessagePackObject]
        public class SetTrainPlatformTransferModeRequest : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack Position { get; set; }
            [Key(3)] public TrainPlatformTransferComponent.TransferMode Mode { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public SetTrainPlatformTransferModeRequest()
            {
            }

            public SetTrainPlatformTransferModeRequest(Vector3Int position, TrainPlatformTransferComponent.TransferMode mode)
            {
                Tag = ProtocolTag;
                Position = new Vector3IntMessagePack(position);
                Mode = mode;
            }
        }

        [MessagePackObject]
        public class SetTrainPlatformTransferModeResponse : ProtocolMessagePackBase
        {
            [Key(2)] public bool Success { get; set; }
            [Key(3)] public TrainPlatformTransferComponent.TransferMode AppliedMode { get; set; }
            [Key(4)] public SetTrainPlatformTransferModeFailureReason FailureReason { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public SetTrainPlatformTransferModeResponse()
            {
            }

            public SetTrainPlatformTransferModeResponse(bool success, TrainPlatformTransferComponent.TransferMode appliedMode, SetTrainPlatformTransferModeFailureReason failureReason)
            {
                Tag = ProtocolTag;
                Success = success;
                AppliedMode = appliedMode;
                FailureReason = failureReason;
            }
        }

        public enum SetTrainPlatformTransferModeFailureReason
        {
            None,
            BlockNotFound,
            NotTrainPlatform,
        }

        #endregion
    }
}
