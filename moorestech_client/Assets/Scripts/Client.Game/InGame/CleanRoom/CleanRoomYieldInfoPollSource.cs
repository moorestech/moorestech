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
    ///     抽選の中身の照会の1回分の問い合わせと、役割ごとの型への読み替え
    ///     One round of the draw-detail query and its conversion into the per-role view
    /// </summary>
    public class CleanRoomYieldInfoPollSource : IBlockInfoPollSource<GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack, CleanRoomYieldView>
    {
        // 開いている間の1秒照会で部屋クラス・装着・レシピの変化を追う（ユーザー裁定 .decisions/2026-10-25-S1最終レビューの設計判断はすべて推奨案で直す.md D08）
        // Follows room class, module and recipe changes by 1-second polling while open (user ruling D08 in .decisions/2026-10-25-S1最終レビューの設計判断はすべて推奨案で直す.md)
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

        public static BlockInfoPoller<GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack, CleanRoomYieldView> CreatePoller()
        {
            return new BlockInfoPoller<GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack, CleanRoomYieldView>(new CleanRoomYieldInfoPollSource(), "GetCleanRoomYieldInfo", PollInterval);
        }

        public UniTask<GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack> Request(BlockInstanceId blockInstanceId, CancellationToken ct)
        {
            return ClientContext.VanillaApi.Response.World.GetCleanRoomYieldInfo(blockInstanceId, ct);
        }

        public BlockInfoReadResult TryRead(GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack response, out CleanRoomYieldView snapshot)
        {
            return response.TryReadView(out snapshot);
        }
    }
}
