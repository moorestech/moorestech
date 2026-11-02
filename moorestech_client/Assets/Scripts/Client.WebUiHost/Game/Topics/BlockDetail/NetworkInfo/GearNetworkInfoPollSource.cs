using System;
using System.Threading;
using Client.Game.InGame.BlockInfoPolling;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;

namespace Client.WebUiHost.Game.Topics.BlockDetail.NetworkInfo
{
    /// <summary>
    ///     歯車網の集約情報の1回分の問い合わせ。網に属さないことも取得済みの値として保つ
    ///     One round of the gear network aggregate query; non-membership is kept as a fetched value too
    /// </summary>
    public class GearNetworkInfoPollSource : IBlockInfoPollSource<GetGearNetworkInfoProtocol.ResponseGetGearNetworkInfoMessagePack, GetGearNetworkInfoProtocol.GearNetworkInfoSnapshot>
    {
        // 電力網と同じ1秒間隔（開いている間ずっと停止理由行を追随させる）
        // The same 1-second interval as the electric network, so the stop-reason row stays live while open
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

        public static BlockInfoPoller<GetGearNetworkInfoProtocol.ResponseGetGearNetworkInfoMessagePack, GetGearNetworkInfoProtocol.GearNetworkInfoSnapshot> CreatePoller()
        {
            return new BlockInfoPoller<GetGearNetworkInfoProtocol.ResponseGetGearNetworkInfoMessagePack, GetGearNetworkInfoProtocol.GearNetworkInfoSnapshot>(new GearNetworkInfoPollSource(), "GetGearNetworkInfo", PollInterval);
        }

        public UniTask<GetGearNetworkInfoProtocol.ResponseGetGearNetworkInfoMessagePack> Request(BlockInstanceId blockInstanceId, CancellationToken ct)
        {
            return ClientContext.VanillaApi.Response.World.GetGearNetworkInfo(blockInstanceId, ct);
        }

        // 網に属さない（Info=null）は取得済みの「未所属」として返す。通信失敗（応答 null）はポーラーが前回値を保つので混ざらない
        // Non-membership (Info == null) is returned as a fetched "not a member"; a transport failure (null response) keeps the last value in the poller, so the two never mix
        // silent-ok: サーバーはブロックが無いときも同じ Info=null を返すので消失を区別せず未所属として出す / ブロックが消えれば開いている画面ごと閉じ、未所属の表示は画面で見える
        public BlockInfoReadResult TryRead(GetGearNetworkInfoProtocol.ResponseGetGearNetworkInfoMessagePack response, out GetGearNetworkInfoProtocol.GearNetworkInfoSnapshot snapshot)
        {
            snapshot = response.Info;
            return BlockInfoReadResult.Success;
        }
    }
}
