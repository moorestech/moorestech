using System;
using Game.Block.Blocks.ElectricToGear;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class SetElectricToGearOutputModeProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:setElectricToGearOutputMode";

        public SetElectricToGearOutputModeProtocol(ServiceProvider serviceProvider)
        {
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<SetElectricToGearOutputModeRequest>(payload);

            // 指定座標のブロックを取得
            // Fetch the block at the requested position
            var block = ServerContext.WorldBlockDatastore.GetBlock(request.Position.Vector3Int);
            if (block == null)
            {
                return new SetElectricToGearOutputModeResponse(false, request.Index, SetElectricToGearOutputModeFailureReason.BlockNotFound);
            }

            // ElectricToGear コンポーネントを取り出す
            // Fetch the ElectricToGear component
            if (!block.ComponentManager.TryGetComponent<ElectricToGearGeneratorComponent>(out var component))
            {
                return new SetElectricToGearOutputModeResponse(false, request.Index, SetElectricToGearOutputModeFailureReason.NotElectricToGear);
            }

            // モード切替を試みる。範囲外 index は適用されず false が返る。
            // Try to switch the mode; out-of-range index is not applied and returns false.
            if (!component.SetSelectedMode(request.Index))
            {
                return new SetElectricToGearOutputModeResponse(false, component.SelectedIndex, SetElectricToGearOutputModeFailureReason.InvalidIndex);
            }

            return new SetElectricToGearOutputModeResponse(true, component.SelectedIndex, SetElectricToGearOutputModeFailureReason.None);
        }
    }

    public enum SetElectricToGearOutputModeFailureReason
    {
        None,
        BlockNotFound,
        NotElectricToGear,
        InvalidIndex,
    }

    [MessagePackObject]
    public class SetElectricToGearOutputModeRequest : ProtocolMessagePackBase
    {
        [Key(2)] public Vector3IntMessagePack Position { get; set; }
        [Key(3)] public int Index { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public SetElectricToGearOutputModeRequest()
        {
        }

        public SetElectricToGearOutputModeRequest(Vector3Int position, int index)
        {
            Tag = SetElectricToGearOutputModeProtocol.ProtocolTag;
            Position = new Vector3IntMessagePack(position);
            Index = index;
        }
    }

    [MessagePackObject]
    public class SetElectricToGearOutputModeResponse : ProtocolMessagePackBase
    {
        [Key(2)] public bool Success { get; set; }
        [Key(3)] public int AppliedIndex { get; set; }
        [Key(4)] public SetElectricToGearOutputModeFailureReason FailureReason { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public SetElectricToGearOutputModeResponse()
        {
        }

        public SetElectricToGearOutputModeResponse(bool success, int appliedIndex, SetElectricToGearOutputModeFailureReason failureReason)
        {
            Tag = SetElectricToGearOutputModeProtocol.ProtocolTag;
            Success = success;
            AppliedIndex = appliedIndex;
            FailureReason = failureReason;
        }
    }
}
