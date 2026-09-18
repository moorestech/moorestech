using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.BugReport.Submit;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Steam;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Client.Tests.PlaytestReceiver
{
    // 受け口とSteamの差し替え。テストはこれらだけを使い、ネットワークにもSteamにも触れない
    // Stand-ins for the receiver and Steam; the tests use only these and touch neither the network nor Steam
    internal sealed class FakeTicketProvider : IPlaytestSteamTicketProvider
    {
        private readonly string _ticketHex;
        public FakeTicketProvider(string ticketHex) { _ticketHex = ticketHex; }
        public bool IsSteamRunning() { return true; }
        public UniTask<string> RequestWebApiTicketHexAsync(CancellationToken token) { return UniTask.FromResult(_ticketHex); }
        public void ReleaseWebApiTicket() { }
    }

    // チケットを返す時刻をテストが決める。1本目を待たせたまま2本目を呼ぶために使う
    // The test decides when the ticket arrives, so a second call can start while the first is still waiting
    internal sealed class GatedTicketProvider : IPlaytestSteamTicketProvider
    {
        private readonly UniTaskCompletionSource<string> _gate;
        public GatedTicketProvider(UniTaskCompletionSource<string> gate) { _gate = gate; }
        public bool IsSteamRunning() { return true; }
        public UniTask<string> RequestWebApiTicketHexAsync(CancellationToken token) { return _gate.Task; }
        public void ReleaseWebApiTicket() { }
    }

    // チケット解放の順序をテストが見えるように記録する。ReleaseWebApiTicketがPostSessionAsyncより前に来ていないかを固定する
    // Records ticket-release ordering so a test can pin ReleaseWebApiTicket to never precede PostSessionAsync
    internal sealed class TrackingTicketProvider : IPlaytestSteamTicketProvider
    {
        private readonly string _ticketHex;
        private readonly List<string> _events;
        public TrackingTicketProvider(string ticketHex, List<string> events) { _ticketHex = ticketHex; _events = events; }
        public bool IsSteamRunning() { return true; }
        public UniTask<string> RequestWebApiTicketHexAsync(CancellationToken token) { return UniTask.FromResult(_ticketHex); }
        public void ReleaseWebApiTicket() { _events.Add("release"); }
    }

    // 押し場が何回押したかだけを記録する。送信起動箇所のテストで使う
    // Records only how many times a push site pushed; used by the tests of the upload trigger sites
    internal sealed class RecordingUploadRequester : IPlaytestUploadRequester
    {
        public int RequestCount;
        public void RequestUpload() { RequestCount++; }
    }

    internal static class PlaytestSessionBodies
    {
        public const string AllowedFarFuture = "{\"steamId\":\"7656\",\"allowed\":true,\"token\":\"tok\",\"expiresAt\":\"2999-01-01T00:00:00.000Z\"}";

        public static string Allowed(string token, string expiresAt)
        {
            return $"{{\"steamId\":\"7656\",\"allowed\":true,\"token\":\"{token}\",\"expiresAt\":\"{expiresAt}\"}}";
        }
    }

    internal sealed class FakeApi : IPlaytestReceiverApi
    {
        public readonly List<PlaytestApiResult> SessionResponses = new();
        public readonly List<string> Events = new();
        public int SessionCallCount;

        // 実装と同じく打ち切りは例外で伝える。畳んで結果にすると打ち切り経路の挙動を検証できない
        // Cancellation surfaces as an exception just like the real client; folding it into a result would hide that path
        public UniTask<PlaytestApiResult> PostSessionAsync(string ticketHex, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Events.Add("post-session");
            var response = SessionResponses[SessionCallCount];
            SessionCallCount++;
            return UniTask.FromResult(response);
        }

        public UniTask<PlaytestApiResult> PostPrepareAsync(string bearerToken, PlaytestUploadKind kind, string bundleId, IReadOnlyList<PlaytestDeclaredFile> files, CancellationToken token)
        {
            return UniTask.FromResult(PlaytestApiResult.Responded(200, "{}"));
        }

        public UniTask<PlaytestApiResult> PutToSignedUrlAsync(string signedUrl, string absoluteFilePath, long bytes, CancellationToken token)
        {
            return UniTask.FromResult(PlaytestApiResult.Responded(200, ""));
        }

        public UniTask<PlaytestApiResult> PostCompleteAsync(string bearerToken, PlaytestUploadKind kind, string bundleId, string supplementJson, CancellationToken token)
        {
            return UniTask.FromResult(PlaytestApiResult.Responded(200, "{}"));
        }
    }

    // アップロード用フェイク／呼出順をCallsへ記録／空キューは既定成功応答
    // Upload fake; records call order in Calls; empty queue answers with default success
    internal sealed class FakeUploadApi : IPlaytestReceiverApi
    {
        public readonly List<string> Calls = new();
        public int SessionCallCount;
        public int PutAttemptCount;
        public int CompleteCount;
        public string LastCompleteBody = "";
        public UniTaskCompletionSource<PlaytestApiResult> PendingPut;
        public PlaytestApiResult SessionResult = PlaytestApiResult.Responded(200, PlaytestSessionBodies.AllowedFarFuture);

        private readonly Queue<PlaytestApiResult> _prepareResults = new();
        private readonly Dictionary<string, Queue<PlaytestApiResult>> _putResults = new();
        private readonly Queue<PlaytestApiResult> _completeResults = new();

        public void EnqueuePrepare(PlaytestApiResult result) { _prepareResults.Enqueue(result); }
        public void EnqueueComplete(PlaytestApiResult result) { _completeResults.Enqueue(result); }

        public void EnqueuePut(string path, PlaytestApiResult result)
        {
            if (!_putResults.TryGetValue(path, out var queue)) _putResults[path] = queue = new Queue<PlaytestApiResult>();
            queue.Enqueue(result);
        }

        public UniTask<PlaytestApiResult> PostSessionAsync(string ticketHex, CancellationToken token)
        {
            SessionCallCount++;
            return UniTask.FromResult(SessionResult);
        }

        public UniTask<PlaytestApiResult> PostPrepareAsync(string bearerToken, PlaytestUploadKind kind, string bundleId, IReadOnlyList<PlaytestDeclaredFile> files, CancellationToken token)
        {
            Calls.Add("prepare");
            return UniTask.FromResult(_prepareResults.Count != 0 ? _prepareResults.Dequeue() : PlaytestApiResult.Responded(200, PrepareBodyFor(files)));
        }

        // 既定のprepare応答が返すURLは https://r2.test/<path> なので、URLから宣言のパスへ戻せる
        // The default prepare answer uses https://r2.test/<path>, so the declared path is recovered from the URL
        public UniTask<PlaytestApiResult> PutToSignedUrlAsync(string signedUrl, string absoluteFilePath, long bytes, CancellationToken token)
        {
            var path = signedUrl.Substring(SignedUrlPrefix.Length);
            Calls.Add($"put:{path}");
            PutAttemptCount++;
            if (PendingPut != null) return PendingPut.Task;
            var queued = _putResults.TryGetValue(path, out var queue) && queue.Count != 0;
            return UniTask.FromResult(queued ? queue.Dequeue() : PlaytestApiResult.Responded(200, ""));
        }

        public UniTask<PlaytestApiResult> PostCompleteAsync(string bearerToken, PlaytestUploadKind kind, string bundleId, string supplementJson, CancellationToken token)
        {
            Calls.Add("complete");
            CompleteCount++;
            LastCompleteBody = supplementJson;
            return UniTask.FromResult(_completeResults.Count != 0 ? _completeResults.Dequeue() : PlaytestApiResult.Responded(200, "{}"));
        }

        private const string SignedUrlPrefix = "https://r2.test/";

        private static string PrepareBodyFor(IReadOnlyList<PlaytestDeclaredFile> files)
        {
            var uploads = new JArray();
            foreach (var f in files) uploads.Add(new JObject { ["path"] = f.Path, ["url"] = SignedUrlPrefix + f.Path, ["bytes"] = f.Bytes });
            return new JObject { ["outcome"] = "prepared", ["uploads"] = uploads, ["expiresInSeconds"] = 3600 }.ToString(Formatting.None);
        }
    }
}
