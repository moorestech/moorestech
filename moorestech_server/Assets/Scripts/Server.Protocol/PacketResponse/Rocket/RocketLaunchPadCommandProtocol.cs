using System;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     発射台への建造・発射の合図。結果の表示はブロック状態の更新を正とする
    ///     Build/launch commands for a launch pad; the displayed result follows the block state update
    /// </summary>
    public class RocketLaunchPadCommandProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:rocketLaunchPadCommand";

        public RocketLaunchPadCommandProtocol(ServiceProvider serviceProvider)
        {
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, int requesterPlayerId)
        {
            var request = MessagePackSerializer.Deserialize<RocketLaunchPadCommandRequest>(payload);
            if (request.Position == null || !request.Operation.HasValue || !request.ExpectedInstanceId.HasValue) return Fail(RocketLaunchPadCommandFailureReason.InvalidRequest);

            // 指定座標の発射台を引く。無ければ理由を残して拒否する
            // Look up the launch pad at the position, rejecting with a logged reason when absent
            var block = ServerContext.WorldBlockDatastore.GetBlock(request.Position.Vector3Int);
            if (block == null) return Fail(RocketLaunchPadCommandFailureReason.BlockNotFound);
            if (block.BlockInstanceId.AsPrimitive() != request.ExpectedInstanceId.Value) return Fail(RocketLaunchPadCommandFailureReason.InstanceMismatch);
            if (!block.ComponentManager.TryGetComponent<IRocketLaunchPadComponent>(out var launchPad)) return Fail(RocketLaunchPadCommandFailureReason.NotRocketLaunchPad);

            // 操作を発射台へ渡し結果を通信理由へ変換
            // Dispatch operations to the pad and map results to wire failure reasons
            switch (request.Operation)
            {
                case RocketLaunchPadCommandOperation.Build:
                    return ToResponse(launchPad.RequestBuild());
                case RocketLaunchPadCommandOperation.Launch:
                    return ToResponse(launchPad.RequestLaunch());
                default:
                    return Fail(RocketLaunchPadCommandFailureReason.UnknownOperation);
            }

            #region Internal

            RocketLaunchPadCommandResponse ToResponse(RocketLaunchPadCommandResult result)
            {
                // 部品側の拒否は部品が理由をログに出している
                // Component-side rejections are already logged by the component
                if (result == RocketLaunchPadCommandResult.Accepted) return new RocketLaunchPadCommandResponse(true, RocketLaunchPadCommandFailureReason.None);
                return new RocketLaunchPadCommandResponse(false, ToFailureReason(result));
            }

            RocketLaunchPadCommandResponse Fail(RocketLaunchPadCommandFailureReason reason)
            {
                Debug.LogWarning($"ロケット発射台への合図を拒否しました 理由:{reason} 座標:{request.Position?.Vector3Int}");
                return new RocketLaunchPadCommandResponse(false, reason);
            }

            static RocketLaunchPadCommandFailureReason ToFailureReason(RocketLaunchPadCommandResult result)
            {
                return result switch
                {
                    RocketLaunchPadCommandResult.NotAwaitingBuildCommand => RocketLaunchPadCommandFailureReason.NotAwaitingBuildCommand,
                    RocketLaunchPadCommandResult.NotAwaitingLaunchCommand => RocketLaunchPadCommandFailureReason.NotAwaitingLaunchCommand,
                    RocketLaunchPadCommandResult.MissingMaterials => RocketLaunchPadCommandFailureReason.MissingMaterials,
                    RocketLaunchPadCommandResult.MissingPayload => RocketLaunchPadCommandFailureReason.MissingPayload,
                    RocketLaunchPadCommandResult.OutputFull => RocketLaunchPadCommandFailureReason.OutputFull,
                    _ => throw new ArgumentOutOfRangeException(nameof(result), result, "通信の失敗理由へ写していない発射台の結果です"),
                };
            }

            #endregion
        }

        #region MessagePack

        [MessagePackObject]
        public class RocketLaunchPadCommandRequest : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack Position { get; set; }
            [Key(3)] public RocketLaunchPadCommandOperation? Operation { get; set; }
            [Key(4)] public int? ExpectedInstanceId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RocketLaunchPadCommandRequest() { Tag = ProtocolTag; }

            public RocketLaunchPadCommandRequest(Vector3Int position, RocketLaunchPadCommandOperation operation, BlockInstanceId expectedInstanceId)
            {
                Tag = ProtocolTag;
                Position = new Vector3IntMessagePack(position);
                Operation = operation;
                ExpectedInstanceId = expectedInstanceId.AsPrimitive();
            }
        }

        [MessagePackObject]
        public class RocketLaunchPadCommandResponse : ProtocolMessagePackBase
        {
            [Key(2)] public bool Success { get; set; }
            [Key(3)] public RocketLaunchPadCommandFailureReason FailureReason { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RocketLaunchPadCommandResponse() { }

            public RocketLaunchPadCommandResponse(bool success, RocketLaunchPadCommandFailureReason failureReason)
            {
                Tag = ProtocolTag;
                Success = success;
                FailureReason = failureReason;
            }
        }

        public enum RocketLaunchPadCommandOperation
        {
            Build = 0,
            Launch = 1,
        }

        public enum RocketLaunchPadCommandFailureReason
        {
            None = 0,
            BlockNotFound = 1,
            NotRocketLaunchPad = 2,
            UnknownOperation = 3,
            InvalidRequest = 4,
            NotAwaitingBuildCommand = 5,
            NotAwaitingLaunchCommand = 6,
            MissingMaterials = 7,
            MissingPayload = 9,
            InstanceMismatch = 10,
            OutputFull = 8,
        }

        #endregion
    }
}
