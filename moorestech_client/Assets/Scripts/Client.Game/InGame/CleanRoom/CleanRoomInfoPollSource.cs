using System;
using System.Threading;
using Client.Game.InGame.BlockInfoPolling;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.CleanRoom
{
    /// <summary>
    ///     部屋照会の1回分の問い合わせと、BlockNotFound を除いた種別ごとの型への読み替え
    ///     One round of the room query and its conversion into the per-kind view, excluding BlockNotFound
    /// </summary>
    public class CleanRoomInfoPollSource : IBlockInfoPollSource<GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack, CleanRoomInfoView>
    {
        // 部屋の検出・清浄化の進み具合を追う間隔（電力網の ElectricNetworkInfoPollSource と同じ1秒）
        // Interval following room detection and purification progress, the same 1 second as the electric ElectricNetworkInfoPollSource
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

        public static BlockInfoPoller<GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack, CleanRoomInfoView> CreatePoller()
        {
            return new BlockInfoPoller<GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack, CleanRoomInfoView>(new CleanRoomInfoPollSource(), "GetCleanRoomInfo", PollInterval);
        }

        public UniTask<GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack> Request(BlockInstanceId blockInstanceId, CancellationToken ct)
        {
            return ClientContext.VanillaApi.Response.World.GetCleanRoomInfo(blockInstanceId, ct);
        }

        public bool TryGetSnapshot(GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack response, out CleanRoomInfoView snapshot)
        {
            return response.TryGetView(out snapshot);
        }
    }
}
