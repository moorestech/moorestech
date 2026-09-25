using Client.Game.InGame.BugReport.Submit;
using Client.PlaytestReceiver.Launch;
using Client.PlaytestReceiver.Steam;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Upload.Attempt;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.PlaytestReceiver.Upload
{
    // 起動直後と報告送信直後の押し場の受け手。送るかどうかは配布版判定から自分で決め、走行は1本に保つ
    // Receives the post-launch and post-report pushes; it decides from the launch profile whether to ship and keeps runs single
    public sealed class PlaytestUploadRunner : IPlaytestUploadRequester
    {
        // 走行はプロセスで1本。MainMenuとMainGameがそれぞれの合成ルートで組んだ走行役同士でも重ねないため型で共有する
        // One run per process; the flags are shared by type so runners built by the MainMenu and MainGame roots never overlap
        private static bool _running;
        private static bool _rerunRequested;
        private static PlaytestSession _sharedSession;

        private readonly IPlaytestReceiverApi _api;
        private readonly PlaytestOutboxDirectories _directories;

        // Steam境界はDIが注入する。本番はPlaytestSteamTicketProvider、テストは差し替え
        // The Steam boundary is injected by DI; production uses PlaytestSteamTicketProvider, tests substitute it
        public PlaytestUploadRunner(IPlaytestReceiverApi api, PlaytestOutboxDirectories directories, IPlaytestSteamTicketProvider ticketProvider)
        {
            _api = api;
            _directories = directories;
            // 最初の走行役が認証境界を確定し、後続の走行役も期限内のトークンを使う
            // The first runner fixes the authentication boundary; later runners reuse its valid token
            if (_sharedSession == null) _sharedSession = new PlaytestSession(api, ticketProvider);
        }

        // 走行フラグはEditorの再生跨ぎで残る。残したままだと2回目の再生で一度もアップロードが始まらない
        // The in-flight flags would survive between Editor play sessions, and stale ones would stop every later upload
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetOnPlayMode()
        {
            _running = false;
            _rerunRequested = false;
            _sharedSession = null;
        }

        public void RequestUpload()
        {
            // 送るのは配布版だけ。送らない場合も理由をログへ出す
            // Only a distribution build ships, and not shipping is logged too
            if (PlaytestLaunchProfile.Resolve() != PlaytestLaunchKind.Distribution)
            {
                Debug.Log("[PlaytestReceiver] developer mode; outbox boxes are left for the rsync path");
                return;
            }

            if (_running)
            {
                // 走行中の再要求は捨てずに記録する。今回の走行が終わった直後にもう一度走らせる
                // A re-request while running is remembered instead of dropped, and reruns right after the current pass
                _rerunRequested = true;
                Debug.Log("[PlaytestReceiver] an upload run is already in flight; a rerun is scheduled after it finishes");
                return;
            }
            _running = true;
            RunAsync(_sharedSession).Forget();
        }

        private async UniTaskVoid RunAsync(PlaytestSession session)
        {
            // 途中で何が起きても走行フラグを戻し、再走行の要求も例外経路で評価する。取りこぼすと以後のアップロードが止まる
            // The flag is always cleared and a pending rerun is honoured even on the exception path; missing either stops later uploads
            try
            {
                var uploader = new PlaytestUploader(_api, session, _directories, PlaytestUploadRetrySchedule.Default);
                var sent = await uploader.UploadPendingAsync(Application.exitCancellationToken);
                Debug.Log($"[PlaytestReceiver] upload run finished: {sent} box(es) sent");
            }
            finally
            {
                _running = false;
                if (_rerunRequested)
                {
                    _rerunRequested = false;
                    RequestUpload();
                }
            }
        }
    }
}
