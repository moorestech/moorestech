using Client.PlaytestReceiver.Credentials;
using Client.Game.InGame.BugReport.Submit;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Steam;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Upload.Attempt;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.PlaytestReceiver.Upload
{
    // 起動直後と報告送信直後の押し場の受け手。経路を解決し、走行は1本に保つ
    // Receives the post-launch and post-report pushes; it resolves the delivery route and keeps runs single
    public sealed class PlaytestUploadRunner : IPlaytestUploadRequester
    {
        // 走行はプロセスで1本。MainMenuとMainGameがそれぞれの合成ルートで組んだ走行役同士でも重ねないため型で共有する
        // One run per process; the flags are shared by type so runners built by the MainMenu and MainGame roots never overlap
        private static bool _running;
        private static bool _rerunRequested;
        private static PlaytestDeliveryRoute _rerunRoute;
        private static PlaytestSession _sharedSession;
        private static PlaytestSession _developerSession;
        private static string _developerSessionToken;
        private readonly IPlaytestSteamTicketProvider _ticketProvider;

        private readonly IPlaytestReceiverApi _api;
        private readonly PlaytestOutboxDirectories _directories;

        // Steam境界はDIが注入する。本番はPlaytestSteamTicketProvider、テストは差し替え
        // The Steam boundary is injected by DI; production uses PlaytestSteamTicketProvider, tests substitute it
        public PlaytestUploadRunner(IPlaytestReceiverApi api, PlaytestOutboxDirectories directories, IPlaytestSteamTicketProvider ticketProvider)
        {
            _ticketProvider = ticketProvider;
            _api = api;
            _directories = directories;
        }

        // 走行フラグはEditorの再生跨ぎで残る。残したままだと2回目の再生で一度もアップロードが始まらない
        // The in-flight flags would survive between Editor play sessions, and stale ones would stop every later upload
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetOnPlayMode()
        {
            _running = false;
            _rerunRequested = false;
            _rerunRoute = null;
            _sharedSession = null;
            _developerSession = null;
            _developerSessionToken = null;
        }

        public void RequestUpload()
        {
            RequestUploadWith(PlaytestDeliveryRouteResolver.ResolveCurrent());
        }

        internal void RequestUploadWith(PlaytestDeliveryRoute route)
        {
            // 送信しない場合も理由と対象件数を画面・ログへ出す
            // Publish the reason and relevant count to the UI and log when staying local
            if (route is PlaytestLocalOnlyRoute local)
            {
                PlaytestDeliveryStatusBoard.Set(new PlaytestDeliveryLocalOnly(local.Reason, PlaytestOutboxScanner.ScanPending(_directories, local.Scope).Count));
                Debug.Log($"[PlaytestReceiver] boxes stay local: {local.Detail}");
                return;
            }

            if (_running)
            {
                // 走行中の再要求は捨てずに記録する。今回の走行が終わった直後にもう一度走らせる
                // A re-request while running is remembered instead of dropped, and reruns right after the current pass
                _rerunRequested = true;
                _rerunRoute = route;
                Debug.Log("[PlaytestReceiver] an upload run is already in flight; a rerun is scheduled after it finishes");
                return;
            }
            _running = true;
            RunAsync(route).Forget();
        }

        private async UniTaskVoid RunAsync(PlaytestDeliveryRoute route)
        {
            // 途中で何が起きても走行フラグを戻し、再走行の要求も例外経路で評価する。取りこぼすと以後のアップロードが止まる
            // The flag is always cleared and a pending rerun is honoured even on the exception path; missing either stops later uploads
            try
            {
                PlaytestDeliveryStatusBoard.Set(new PlaytestDeliveryPending(PlaytestOutboxScanner.ScanPending(_directories, route.Scope).Count));
                var uploader = new PlaytestUploader(_api, ResolveSession(route), _directories, PlaytestUploadRetrySchedule.Default, route.Scope);
                var summary = await uploader.UploadPendingAsync(Application.exitCancellationToken);
                var remaining = PlaytestOutboxScanner.ScanPending(_directories, route.Scope).Count;
                PlaytestDeliveryStatusBoard.Set(summary.End == PlaytestUploadRunEnd.SessionRefused
                    ? new PlaytestDeliveryRefused(remaining)
                    : remaining == 0 ? PlaytestDeliveryAllSent.Instance : new PlaytestDeliveryPending(remaining));
                Debug.Log($"[PlaytestReceiver] upload run finished: {summary.Sent} box(es) sent");
            }
            finally
            {
                _running = false;
                if (_rerunRequested)
                {
                    _rerunRequested = false;
                    var rerunRoute = _rerunRoute;
                    _rerunRoute = null;
                    RequestUploadWith(rerunRoute);
                }
            }
        }

        private PlaytestSession ResolveSession(PlaytestDeliveryRoute route)
        {
            // 認証方式と開発者トークンごとに寿命管理を分ける
            // Keep credential lifetimes separate by authentication kind and developer token
            switch (route)
            {
                case PlaytestSteamRoute:
                    return _sharedSession ??= new PlaytestSession(_api, new SteamTicketSessionCredential(_ticketProvider));
                case PlaytestDeveloperTokenRoute developer:
                    if (_developerSession == null || _developerSessionToken != developer.DeveloperToken)
                    {
                        _developerSessionToken = developer.DeveloperToken;
                        _developerSession = new PlaytestSession(_api, new DeveloperTokenSessionCredential(developer.DeveloperToken));
                    }
                    return _developerSession;
                default: throw new System.ArgumentException("A local-only route cannot start an upload", nameof(route));
            }
        }
    }
}
