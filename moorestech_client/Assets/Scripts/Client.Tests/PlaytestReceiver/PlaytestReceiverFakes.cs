using System.Collections.Generic;
using System.Threading;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Steam;
using Cysharp.Threading.Tasks;

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

        public UniTask<PlaytestApiResult> PutFileAsync(string bearerToken, PlaytestUploadKind kind, string bundleId, string relativePath, string absoluteFilePath, CancellationToken token)
        {
            return UniTask.FromResult(PlaytestApiResult.Responded(200, "{}"));
        }

        public UniTask<PlaytestApiResult> PostCompleteAsync(string bearerToken, PlaytestUploadKind kind, string bundleId, string summaryJson, CancellationToken token)
        {
            return UniTask.FromResult(PlaytestApiResult.Responded(200, "{}"));
        }
    }

    // アップロード用。PUTとcompleteの結果をキューで順に返し、呼ばれた回数を数える
    // For uploads; PUT and complete results are dequeued in order and every call is counted
    internal sealed class FakeUploadApi : IPlaytestReceiverApi
    {
        public readonly List<string> PutPaths = new();
        public readonly Queue<PlaytestApiResult> PutResultQueue = new();
        public readonly Queue<PlaytestApiResult> CompleteResultQueue = new();
        public int CompleteCount;
        public int PutAttemptCount;
        public int SessionCallCount;
        public string LastSummary = "";
        public UniTaskCompletionSource<PlaytestApiResult> PendingPut;
        public PlaytestApiResult PutResult = PlaytestApiResult.Responded(200, "{}");
        public PlaytestApiResult SessionResult = PlaytestApiResult.Responded(200, PlaytestSessionBodies.AllowedFarFuture);

        public UniTask<PlaytestApiResult> PostSessionAsync(string ticketHex, CancellationToken token)
        {
            SessionCallCount++;
            return UniTask.FromResult(SessionResult);
        }

        public UniTask<PlaytestApiResult> PutFileAsync(string bearerToken, PlaytestUploadKind kind, string bundleId, string relativePath, string absoluteFilePath, CancellationToken token)
        {
            PutAttemptCount++;
            if (PendingPut != null) return PendingPut.Task;

            var result = PutResultQueue.Count != 0 ? PutResultQueue.Dequeue() : PutResult;
            if (result.IsSuccess) PutPaths.Add(relativePath);
            return UniTask.FromResult(result);
        }

        public UniTask<PlaytestApiResult> PostCompleteAsync(string bearerToken, PlaytestUploadKind kind, string bundleId, string summaryJson, CancellationToken token)
        {
            CompleteCount++;
            LastSummary = summaryJson;
            return UniTask.FromResult(CompleteResultQueue.Count != 0 ? CompleteResultQueue.Dequeue() : PlaytestApiResult.Responded(200, "{}"));
        }
    }
}
