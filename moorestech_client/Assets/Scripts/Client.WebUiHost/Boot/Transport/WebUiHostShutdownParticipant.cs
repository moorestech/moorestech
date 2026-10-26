using Client.Game.Common;
using Cysharp.Threading.Tasks;

namespace Client.WebUiHost.Boot.Transport
{
    // 正規終了はHost停止と実切断結果の集約を待つ
    // Await host shutdown and aggregate actual socket termination during normal exit
    internal sealed class WebUiHostShutdownParticipant : IGameShutdownParticipant
    {
        public async UniTask<ShutdownFlushResult> FlushOnShutdownAsync()
        {
            var result = await WebUiHost.StopAsync();
            return result.Succeeded ? ShutdownFlushResult.Flushed : ShutdownFlushResult.FlushFailed;
        }
    }
}
