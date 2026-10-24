using System;
using System.Threading;
using Client.Game.InGame.BlockInfoPolling;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;

namespace Client.WebUiHost.Game.Topics.BlockDetail.MachineInput
{
    /// <summary>
    ///     止まった理由の照会の1回分の問い合わせと、BlockNotFound を除いた種別ごとの型への読み替え
    ///     One round of the stall query and its conversion into the per-kind view, excluding BlockNotFound
    /// </summary>
    public class MachineInputStallPollSource : IBlockInfoPollSource<GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack, MachineInputStallView>
    {
        // 電力網・部屋の照会と同じ1秒。止まった理由は表示中だけ要る
        // The same 1 second as the electric and room queries; the stall is needed only while displayed
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

        public static BlockInfoPoller<GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack, MachineInputStallView> CreatePoller()
        {
            return new BlockInfoPoller<GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack, MachineInputStallView>(new MachineInputStallPollSource(), "GetMachineInputStall", PollInterval);
        }

        public UniTask<GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack> Request(BlockInstanceId blockInstanceId, CancellationToken ct)
        {
            return ClientContext.VanillaApi.Response.World.GetMachineInputStall(blockInstanceId, ct);
        }

        public bool TryGetSnapshot(GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack response, out MachineInputStallView snapshot)
        {
            return response.TryGetView(out snapshot);
        }
    }
}
