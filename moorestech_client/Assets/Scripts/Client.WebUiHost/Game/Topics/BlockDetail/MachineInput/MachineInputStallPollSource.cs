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
    ///     止まった理由の照会の1回分の問い合わせと、種別ごとの型への読み替え
    ///     One round of the stall query and its conversion into the per-kind view
    /// </summary>
    public class MachineInputStallPollSource : IBlockInfoPollSource<GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack, MachineInputStallView>
    {
        // 止まった理由は開いている間だけ1秒照会する（ユーザー裁定 .decisions/2026-10-25-S1最終レビューの設計判断はすべて推奨案で直す.md D08）
        // The stall is polled every 1 second only while open (user ruling D08 in .decisions/2026-10-25-S1最終レビューの設計判断はすべて推奨案で直す.md)
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

        public static BlockInfoPoller<GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack, MachineInputStallView> CreatePoller()
        {
            return new BlockInfoPoller<GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack, MachineInputStallView>(new MachineInputStallPollSource(), "GetMachineInputStall", PollInterval);
        }

        public UniTask<GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack> Request(BlockInstanceId blockInstanceId, CancellationToken ct)
        {
            return ClientContext.VanillaApi.Response.World.GetMachineInputStall(blockInstanceId, ct);
        }

        public BlockInfoReadResult TryRead(GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack response, out MachineInputStallView snapshot)
        {
            return response.TryReadView(out snapshot);
        }
    }
}
