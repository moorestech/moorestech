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
    ///     電力網の集約情報の1回分の問い合わせ。網に属さないことも取得済みの値として保つ
    ///     One round of the electric network aggregate query; non-membership is kept as a fetched value too
    /// </summary>
    public class ElectricNetworkInfoPollSource : IBlockInfoPollSource<GetElectricNetworkInfoProtocol.ResponseGetElectricNetworkInfoMessagePack, GetElectricNetworkInfoProtocol.ElectricNetworkInfoSnapshot>
    {
        // uGUI ElectricNetworkInfoView と同じ1秒間隔
        // The same 1-second interval as uGUI ElectricNetworkInfoView
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

        public static BlockInfoPoller<GetElectricNetworkInfoProtocol.ResponseGetElectricNetworkInfoMessagePack, GetElectricNetworkInfoProtocol.ElectricNetworkInfoSnapshot> CreatePoller()
        {
            return new BlockInfoPoller<GetElectricNetworkInfoProtocol.ResponseGetElectricNetworkInfoMessagePack, GetElectricNetworkInfoProtocol.ElectricNetworkInfoSnapshot>(new ElectricNetworkInfoPollSource(), "GetElectricNetworkInfo", PollInterval);
        }

        public UniTask<GetElectricNetworkInfoProtocol.ResponseGetElectricNetworkInfoMessagePack> Request(BlockInstanceId blockInstanceId, CancellationToken ct)
        {
            return ClientContext.VanillaApi.Response.World.GetElectricNetworkInfo(blockInstanceId, ct);
        }

        // 網に属さない（Info=null）は取得済みの「未所属」として返す。通信失敗（応答 null）はポーラーが前回値を保つので混ざらない
        // Non-membership (Info == null) is returned as a fetched "not a member"; a transport failure (null response) keeps the last value in the poller, so the two never mix
        // silent-ok: サーバーはブロックが無いときも同じ Info=null を返すので消失を区別せず未所属として出す / ブロックが消えれば開いている画面ごと閉じ、未所属の表示は画面で見える
        public bool TryGetSnapshot(GetElectricNetworkInfoProtocol.ResponseGetElectricNetworkInfoMessagePack response, out GetElectricNetworkInfoProtocol.ElectricNetworkInfoSnapshot snapshot)
        {
            snapshot = response.Info;
            return true;
        }
    }
}
