using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Client.PlaytestReceiver.Http
{
    // 受け口API。バイト列はR2署名URLへ送る
    // The receiver API; bytes go to the R2 presigned URL
    public interface IPlaytestReceiverApi
    {
        UniTask<PlaytestApiResult> PostSessionAsync(string ticketHex, CancellationToken token);
        UniTask<PlaytestApiResult> PostPrepareAsync(string bearerToken, PlaytestUploadKind kind, string bundleId, int generation, IReadOnlyList<PlaytestDeclaredFile> files, CancellationToken token);
        UniTask<PlaytestApiResult> PutToSignedUrlAsync(string signedUrl, string absoluteFilePath, long bytes, CancellationToken token);
        UniTask<PlaytestApiResult> PostCompleteAsync(string bearerToken, PlaytestUploadKind kind, string bundleId, string supplementJson, CancellationToken token);
    }
}
