using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;

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

        // ブロック消失（または読めない応答）なら false。ポーラーは値を捨てて問い合わせを止める
        // False when the block has vanished (or the response is unreadable); the poller drops the value and stops polling
        bool TryGetSnapshot(TResponse response, out TSnapshot snapshot);
    }
}
