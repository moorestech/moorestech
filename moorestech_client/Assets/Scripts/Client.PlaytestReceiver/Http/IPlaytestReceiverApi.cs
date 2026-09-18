using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Client.PlaytestReceiver.Http
{
    // 受け口への呼び出し面。バイト列は受け口ではなく署名付きURL（R2）へ送る
    // The receiver call surface; bytes go to the presigned URL (R2), not to the receiver
    public interface IPlaytestReceiverApi
    {
        UniTask<PlaytestApiResult> PostSessionAsync(string ticketHex, CancellationToken token);
        UniTask<PlaytestApiResult> PostPrepareAsync(string bearerToken, PlaytestUploadKind kind, string bundleId, IReadOnlyList<PlaytestDeclaredFile> files, CancellationToken token);
        UniTask<PlaytestApiResult> PutToSignedUrlAsync(string signedUrl, string absoluteFilePath, long bytes, CancellationToken token);
        UniTask<PlaytestApiResult> PostCompleteAsync(string bearerToken, PlaytestUploadKind kind, string bundleId, string supplementJson, CancellationToken token);
    }
}
