using System.Collections.Generic;
using System.Threading;
using Client.PlaytestReceiver.Http;
using Cysharp.Threading.Tasks;

namespace Client.PlaytestReceiver.Upload
{
    // 箱の宣言を送りURLを受ける。トークンの取り直しと再送はセッション側が行う
    // Sends the box declaration and receives the URLs; the session performs any token refresh and resend
    internal sealed class PlaytestPrepareCall : IPlaytestAuthorizedCall
    {
        private readonly IPlaytestReceiverApi _api;
        private readonly PlaytestOutboxBox _box;
        private readonly IReadOnlyList<PlaytestDeclaredFile> _files;

        public PlaytestPrepareCall(IPlaytestReceiverApi api, PlaytestOutboxBox box, IReadOnlyList<PlaytestDeclaredFile> files)
        {
            _api = api;
            _box = box;
            _files = files;
        }

        public UniTask<PlaytestApiResult> SendAsync(string bearerToken, CancellationToken token)
        {
            return _api.PostPrepareAsync(bearerToken, _box.Kind, _box.BundleId, _files, token);
        }
    }

    // 箱の完了通知。要約JSONを添えて送る
    // The box completion notice, carrying the summary JSON
    internal sealed class PlaytestCompleteCall : IPlaytestAuthorizedCall
    {
        private readonly IPlaytestReceiverApi _api;
        private readonly PlaytestOutboxBox _box;
        private readonly string _summaryJson;

        public PlaytestCompleteCall(IPlaytestReceiverApi api, PlaytestOutboxBox box, string summaryJson)
        {
            _api = api;
            _box = box;
            _summaryJson = summaryJson;
        }

        public UniTask<PlaytestApiResult> SendAsync(string bearerToken, CancellationToken token)
        {
            return _api.PostCompleteAsync(bearerToken, _box.Kind, _box.BundleId, _summaryJson, token);
        }
    }
}
