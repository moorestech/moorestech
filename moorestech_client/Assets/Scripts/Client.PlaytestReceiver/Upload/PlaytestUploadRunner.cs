using System;
using Client.PlaytestReceiver.Http;
using Cysharp.Threading.Tasks;
using Game.Paths;
using UnityEngine;

namespace Client.PlaytestReceiver.Upload
{
    // 起動直後と報告送信直後の2箇所から呼ばれる入口。走行中の再要求は無視して1本に保つ
    // The entry point called right after launch and right after a report; re-requests while running are ignored
    public sealed class PlaytestUploadRunner
    {
        private static PlaytestUploadRunner _instance;

        // 実パスと実クライアントを掴むのは初回の要求時だけ。テストは自前の引数で組むのでここには来ない
        // The real paths and client are taken only on the first request; tests build their own and never reach here
        public static PlaytestUploadRunner Instance => _instance ??= new PlaytestUploadRunner(
            new PlaytestReceiverClient(PlaytestReceiverConfig.BaseUrl),
            GameSystemPaths.BugReportOutboxDirectory,
            GameSystemPaths.ProgressRecordOutboxDirectory);

        private readonly IPlaytestReceiverApi _api;
        private readonly string _reportOutbox;
        private readonly string _progressOutbox;

        private bool _running;

        public PlaytestUploadRunner(IPlaytestReceiverApi api, string reportOutbox, string progressOutbox)
        {
            _api = api;
            _reportOutbox = reportOutbox;
            _progressOutbox = progressOutbox;
        }

        // 走行フラグはEditorの再生跨ぎで残る。残したままだと2回目の再生で一度もアップロードが始まらない
        // The in-flight flag would survive between Editor play sessions, and a stale one would stop every later upload
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayMode()
        {
            _instance = null;
        }

        public void RequestUpload(PlaytestSession session)
        {
            if (_running)
            {
                Debug.Log("[PlaytestReceiver] an upload run is already in flight; this request rides on it");
                return;
            }
            _running = true;
            RunAsync(session).Forget();
        }

        private async UniTaskVoid RunAsync(PlaytestSession session)
        {
            // 途中で何が起きても走行フラグを必ず戻す。戻し損ねると以後のアップロードが恒久停止する
            // The in-flight flag is always cleared; leaking it would permanently stop every later upload
            try
            {
                var uploader = new PlaytestUploader(_api, session, _reportOutbox, _progressOutbox);
                var sent = await uploader.UploadPendingAsync(DateTime.UtcNow, Application.exitCancellationToken);
                Debug.Log($"[PlaytestReceiver] upload run finished: {sent} box(es) sent");
            }
            finally
            {
                _running = false;
            }
        }
    }
}
