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
    ///     抽選の中身の照会の1回分の問い合わせと、BlockNotFound を除いた役割ごとの型への読み替え
    ///     One round of the draw-detail query and its conversion into the per-role view, excluding BlockNotFound
    /// </summary>
    public class CleanRoomYieldInfoPollSource : IBlockInfoPollSource<GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack, CleanRoomYieldView>
    {
        // 部屋クラス・装着モジュール・選択レシピの変化を追う間隔（部屋照会の CleanRoomInfoPollSource と同じ1秒）
        // Interval following room class, module and recipe changes, the same 1 second as the room query's CleanRoomInfoPollSource
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

        public static BlockInfoPoller<GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack, CleanRoomYieldView> CreatePoller()
        {
            return new BlockInfoPoller<GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack, CleanRoomYieldView>(new CleanRoomYieldInfoPollSource(), "GetCleanRoomYieldInfo", PollInterval);
        }

        public UniTask<GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack> Request(BlockInstanceId blockInstanceId, CancellationToken ct)
        {
            return ClientContext.VanillaApi.Response.World.GetCleanRoomYieldInfo(blockInstanceId, ct);
        }

        public bool TryGetSnapshot(GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack response, out CleanRoomYieldView snapshot)
        {
            return response.TryGetView(out snapshot);
        }
    }
}
