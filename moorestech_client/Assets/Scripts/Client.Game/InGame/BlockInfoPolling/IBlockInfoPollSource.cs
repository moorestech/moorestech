using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockInfoPolling
{
    /// <summary>
    ///     問い合わせと表示値変換
    ///     Requests and display-value conversion
    /// </summary>
    public interface IBlockInfoPollSource<TResponse, TSnapshot>
    {
        // 通信失敗は null を返す（WorldQueryApi の既存の約束）
        // Returns null on a transport failure, as WorldQueryApi already does
        UniTask<TResponse> Request(BlockInstanceId blockInstanceId, CancellationToken ct);

        // 値は Success のときだけ入る。ポーラーは BlockNotFound だけで問い合わせを止め、Unreadable・UnsupportedTarget は値を捨てて再試行する
        // The value is set only on Success; the poller stops only on BlockNotFound and drops the value and retries on Unreadable or UnsupportedTarget
        BlockInfoReadResult TryRead(TResponse response, out TSnapshot snapshot);
    }
}
