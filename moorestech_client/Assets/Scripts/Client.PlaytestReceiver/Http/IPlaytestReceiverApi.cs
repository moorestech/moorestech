using System.Threading;
using Cysharp.Threading.Tasks;

namespace Client.PlaytestReceiver.Http
{
    // 受け口への3つの呼び出し。テストはこの面をフェイクに差し替えてネットワークを踏まない
    // The three receiver calls; tests fake this face so they never touch the network
    public interface IPlaytestReceiverApi
    {
        UniTask<PlaytestApiResult> PostSessionAsync(string ticketHex, CancellationToken token);
        UniTask<PlaytestApiResult> PutFileAsync(string bearerToken, string kind, string bundleId, string relativePath, string absoluteFilePath, CancellationToken token);
        UniTask<PlaytestApiResult> PostCompleteAsync(string bearerToken, string kind, string bundleId, string summaryJson, CancellationToken token);
    }
}
