using System;
using Game.Block.Interface;
using Game.EnergySystem;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    // 指定ブロックが属する電力ネットワークの現時点の集約情報を取得するプロトコル
    // Protocol to fetch the current aggregate info of the electric network that a block belongs to
    public class GetElectricNetworkInfoProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getElectricNetInfo";

        private readonly IElectricWireNetworkDatastore _energySegmentDatastore;

        public GetElectricNetworkInfoProtocol(ServiceProvider serviceProvider)
        {
            // 電力はGearのstaticと異なりDIインスタンスから取得して保持
            // Unlike the gear static datastore, resolve and hold the energy datastore from DI
            _energySegmentDatastore = serviceProvider.GetService<IElectricWireNetworkDatastore>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<RequestGetElectricNetworkInfoMessagePack>(payload);

            // 対象ブロックがどの電力セグメントにも属さないなら Info=null を返す
            // Return Info=null when the target block belongs to no energy segment
            if (!_energySegmentDatastore.TryGetEnergySegment(request.BlockInstanceId, out var segment))
            {
                return new ResponseGetElectricNetworkInfoMessagePack(null);
            }

            // tick毎に確定済みの統計をそのまま公開する（要求時の再計算はしない）
            // Expose the per-tick settled statistics as-is (no recomputation on request)
            var statistics = segment.Statistics;
            var snapshot = new ElectricNetworkInfoSnapshot(statistics.TotalGeneratePower, statistics.TotalRequiredPower, statistics.PowerRate, statistics.ConsumerCount);
            return new ResponseGetElectricNetworkInfoMessagePack(snapshot);
        }

        #region MessagePack

        [MessagePackObject]
        public class RequestGetElectricNetworkInfoMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public BlockInstanceId BlockInstanceId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestGetElectricNetworkInfoMessagePack() { }

            public RequestGetElectricNetworkInfoMessagePack(BlockInstanceId blockInstanceId)
            {
                Tag = ProtocolTag;
                BlockInstanceId = blockInstanceId;
            }
        }

        [MessagePackObject]
        public class ResponseGetElectricNetworkInfoMessagePack : ProtocolMessagePackBase
        {
            // Info が null なら対象ブロックはどの電力セグメントにも未所属。見つかった場合のみ下位フィールドが有効
            // Info == null means the block is not a member of any energy segment; inner fields are meaningful only when Info is non-null
            [Key(2)] public ElectricNetworkInfoSnapshot Info { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseGetElectricNetworkInfoMessagePack() { }

            public ResponseGetElectricNetworkInfoMessagePack(ElectricNetworkInfoSnapshot info)
            {
                Tag = ProtocolTag;
                Info = info;
            }
        }

        [MessagePackObject]
        public class ElectricNetworkInfoSnapshot
        {
            [Key(0)] public float TotalGeneratePower { get; set; }
            [Key(1)] public float TotalRequiredPower { get; set; }
            [Key(2)] public float PowerRate { get; set; }
            // 消費者数。0なら「需要なし」を供給率0%(不足)と区別して表示するために使う
            // Consumer count; when 0 it is used to display "no demand" instead of a misleading 0% supply rate
            [Key(3)] public int ConsumerCount { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ElectricNetworkInfoSnapshot() { }

            public ElectricNetworkInfoSnapshot(float totalGeneratePower, float totalRequiredPower, float powerRate, int consumerCount)
            {
                TotalGeneratePower = totalGeneratePower;
                TotalRequiredPower = totalRequiredPower;
                PowerRate = powerRate;
                ConsumerCount = consumerCount;
            }
        }

        #endregion
    }
}
