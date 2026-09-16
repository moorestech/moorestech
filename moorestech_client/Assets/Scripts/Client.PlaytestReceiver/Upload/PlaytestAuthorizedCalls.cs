using System.Threading;
using Client.PlaytestReceiver.Http;
using Cysharp.Threading.Tasks;

namespace Client.PlaytestReceiver.Upload
{
    // ファイル1つのPUT。トークンの取り直しと再送はセッション側が行う
    // A single file PUT; the session performs any token refresh and resend
    internal sealed class PlaytestPutFileCall : IPlaytestAuthorizedCall
    {
        private readonly IPlaytestReceiverApi _api;
        private readonly PlaytestOutboxBox _box;
        private readonly string _relativePath;
        private readonly string _absoluteFilePath;

        public PlaytestPutFileCall(IPlaytestReceiverApi api, PlaytestOutboxBox box, string relativePath, string absoluteFilePath)
        {
            _api = api;
            _box = box;
            _relativePath = relativePath;
            _absoluteFilePath = absoluteFilePath;
        }

        public UniTask<PlaytestApiResult> SendAsync(string bearerToken, CancellationToken token)
        {
            return _api.PutFileAsync(bearerToken, _box.Kind, _box.BundleId, _relativePath, _absoluteFilePath, token);
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
