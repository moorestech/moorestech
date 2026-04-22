using System;
using Game.Block.Interface;
using Game.Gear.Common;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    // 指定ブロックが属するギアネットワークの現時点の集約情報を取得するプロトコル
    // Protocol to fetch the current aggregate info of the gear network that a block belongs to
    public class GetGearNetworkInfoProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getGearNetInfo";

        public GetGearNetworkInfoProtocol(ServiceProvider serviceProvider)
        {
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload)
        {
            var request = MessagePackSerializer.Deserialize<RequestGetGearNetworkInfoMessagePack>(payload);

            // 対象ブロックがギアネットワーク未登録なら Info=null を返す
            // Return Info=null when the target block is not registered in any gear network
            if (!GearNetworkDatastore.TryGetGearNetwork(request.BlockInstanceId, out var network))
            {
                return new ResponseGetGearNetworkInfoMessagePack(null);
            }

            var info = network.CurrentGearNetworkInfo;
            var snapshot = new GearNetworkInfoSnapshot(info.TotalRequiredGearPower, info.TotalGenerateGearPower, info.StopReason);
            return new ResponseGetGearNetworkInfoMessagePack(snapshot);
        }

        #region MessagePack

        [MessagePackObject]
        public class RequestGetGearNetworkInfoMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public BlockInstanceId BlockInstanceId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestGetGearNetworkInfoMessagePack() { }

            public RequestGetGearNetworkInfoMessagePack(BlockInstanceId blockInstanceId)
            {
                Tag = ProtocolTag;
                BlockInstanceId = blockInstanceId;
            }
        }

        [MessagePackObject]
        public class ResponseGetGearNetworkInfoMessagePack : ProtocolMessagePackBase
        {
            // Info が null なら対象ブロックはギアネットワーク未登録。GearNetworkStopReason.None の二重意味を避けるため、
            // 「見つからない」は Info 全体の null で表現し、見つかった場合のみ下位フィールドに意味を持たせる
            // Info == null means the block is not a member of any gear network. Encoding "not found" as a whole-object null avoids the
            // double meaning of GearNetworkStopReason.None; inner fields are meaningful only when Info is non-null
            [Key(2)] public GearNetworkInfoSnapshot Info { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseGetGearNetworkInfoMessagePack() { }

            public ResponseGetGearNetworkInfoMessagePack(GearNetworkInfoSnapshot info)
            {
                Tag = ProtocolTag;
                Info = info;
            }
        }

        [MessagePackObject]
        public class GearNetworkInfoSnapshot
        {
            [Key(0)] public float TotalRequiredGearPower { get; set; }
            [Key(1)] public float TotalGenerateGearPower { get; set; }
            [Key(2)] public GearNetworkStopReason StopReason { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public GearNetworkInfoSnapshot() { }

            public GearNetworkInfoSnapshot(float totalRequiredGearPower, float totalGenerateGearPower, GearNetworkStopReason stopReason)
            {
                TotalRequiredGearPower = totalRequiredGearPower;
                TotalGenerateGearPower = totalGenerateGearPower;
                StopReason = stopReason;
            }
        }

        #endregion
    }
}
