using System;
using System.IO;
using Client.Game.InGame.BugReport.Submit;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Upload;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.PlaytestReceiver.Recovery
{
    // 再送・書き出しの入口。印の変更は配送と同じ門で直列化
    // Entry point for resend and export; marker changes share the delivery gate
    public sealed class PlaytestOutboxRecovery
    {
        private readonly IPlaytestUploadRequester _uploadRequester;
        private readonly PlaytestOutboxDirectories _directories;
        private readonly IPlaytestExportDestination _destination;
        private readonly IPlaytestFileReveal _reveal;

        public PlaytestOutboxRecovery(IPlaytestUploadRequester uploadRequester, PlaytestOutboxDirectories directories, IPlaytestExportDestination destination, IPlaytestFileReveal reveal)
        {
            _uploadRequester = uploadRequester;
            _directories = directories;
            _destination = destination;
            _reveal = reveal;
        }

        public void RequestResendFailed()
        {
            ResendFailedAsync(PlaytestDeliveryRouteResolver.ResolveCurrent()).Forget();
        }

        public void RequestExportUnsent()
        {
            ExportUnsentAsync(CurrentScope()).Forget();
        }

        // 画面の件数と同じ範囲を配送経路から引く
        // Take the scope from the delivery route so targets match the displayed counts
        private static PlaytestOutboxScope CurrentScope()
        {
            return PlaytestDeliveryRouteResolver.ResolveCurrent().Scope;
        }

        internal async UniTask ResendFailedAsync(PlaytestDeliveryRoute route)
        {
            // 送れない経路で印を外すと失敗理由だけが消える。印は残して走行に表示を更新させる
            // Unmarking on a route that cannot send would only erase the failure reason; keep markers and let the run refresh
            if (route is PlaytestLocalOnlyRoute)
            {
                Debug.LogWarning("[PlaytestReceiver] resend keeps failure markers because the delivery route is local-only");
                _uploadRequester.RequestUpload();
                return;
            }
            // 走行中の配送が終わるまで待ってから印を外す。走行が箱を数えている最中に印を変えない
            // Wait for any in-flight run before unmarking so markers never change under a counting run
            await PlaytestOutboxAccess.Gate.WaitAsync(Application.exitCancellationToken);
            try { PlaytestFailedBoxRelease.ReleaseAll(_directories, route.Scope); }
            finally { PlaytestOutboxAccess.Gate.Release(); }
            // 解除できた数に関わらず要求する。走行が件数表示を最新にする
            // Request regardless of the released count; the run refreshes the displayed counts
            _uploadRequester.RequestUpload();
        }

        private async UniTask ExportUnsentAsync(PlaytestOutboxScope scope)
        {
            var zipPath = BeginExport();
            if (zipPath == null) return;
            var exportedAt = DateTime.UtcNow;
            var cancellationToken = Application.exitCancellationToken;
            // 想定外の例外で抜けても書き出し中のまま固めない。理由はForgetのログとPlayer.logに出る
            // Never stay frozen in running after an unexpected exception; its reason reaches the Forget log and Player.log
            try
            {
                // 読むだけなのでGateは取らない。走行と同時に読んでずれても欠落か取り込み側の「取り込み済みは飛ばす」に吸収される
                // Read-only so no gate is taken; a skew against a running upload is absorbed by missing items or import's skip-already-imported
                PlaytestExportStatus packed = null;
                // トークン付きで往復し、非IO例外でもメインへ戻ってから後始末する
                // Round-trip with the token so even a non-IO exception returns to the main thread before cleanup
                try { await UniTask.RunOnThreadPool(() => { packed = PackUnsent(scope, zipPath, exportedAt); }, cancellationToken: cancellationToken); }
                finally { await UniTask.SwitchToMainThread(); }
                cancellationToken.ThrowIfCancellationRequested();
                FinishExport(packed, zipPath);
            }
            finally
            {
                FailIfStillRunning();
            }
        }

        // 二度押し・出力先欠如を止め書き出し中へ。不可ならnull
        // Stops a second press or missing destination, then enters running; null when it cannot
        internal string BeginExport()
        {
            if (PlaytestExportStatusBoard.Current.Value is PlaytestExportRunning)
            {
                Debug.LogWarning("[PlaytestReceiver] export already running; ignoring the second request");
                return null;
            }
            var directory = _destination.ResolveDirectory();
            if (directory == null)
            {
                Fail("desktop folder unavailable");
                return null;
            }
            PlaytestExportStatusBoard.Set(PlaytestExportRunning.Instance);
            return ChooseFreeZipPath(directory);

            #region Internal

            string ChooseFreeZipPath(string folder)
            {
                // 同じ秒の再押下で先にできた正常なzipを上書き・削除しないよう空き名を選ぶ
                // Pick a free name so a same-second second press never touches the zip that already exists
                var stem = $"moorestech-reports-{DateTime.Now:yyyyMMdd-HHmmss}";
                var candidate = Path.Combine(folder, stem + ".zip");
                for (var suffix = 2; File.Exists(candidate); suffix++) candidate = Path.Combine(folder, $"{stem}-{suffix}.zip");
                return candidate;
            }

            #endregion
        }

        // zipを書く。失敗時は途中のzipを消す。任意スレッド可
        // Writes the zip; removes the partial file on failure; callable from any thread
        internal PlaytestExportStatus PackUnsent(PlaytestOutboxScope scope, string zipPath, DateTime exportedAtUtc)
        {
            // 外部ディスクIO境界。他クライアントが同名で先に作ったzipは消さず、作成失敗はそのまま失敗にする
            // External disk IO boundary: a creation failure never deletes a zip another client made first
            FileStream stream;
            try { stream = new FileStream(zipPath, FileMode.CreateNew, FileAccess.Write); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                return new PlaytestExportFailed($"{error.GetType().Name}: {error.Message}");
            }
            // 作成できた後の失敗（容量不足・権限）だけ、自分が作った途中のzipを消す
            // Only failures after our own creation (disk full, permissions) remove our partial zip
            try
            {
                var result = PlaytestUnsentZipWriter.Write(_directories, scope, stream, exportedAtUtc);
                return new PlaytestExportDone(zipPath, result.BoxCount, result.Missing, false);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                stream.Dispose();
                DeletePartial(zipPath);
                return new PlaytestExportFailed($"{error.GetType().Name}: {error.Message}");
            }

            #region Internal

            void DeletePartial(string partialZipPath)
            {
                // 外部ディスクIO境界。消せない途中のzipはログに残し、画面は失敗のまま出す
                // External disk IO boundary: a partial zip that cannot be removed is logged; the screen still shows the failure
                try { if (File.Exists(partialZipPath)) File.Delete(partialZipPath); }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                    Debug.LogWarning($"[PlaytestReceiver] cannot remove partial export {partialZipPath}: {error.Message}");
                }
            }

            #endregion
        }

        // 結果を画面へ出しzipを選択表示。メインスレッド専用
        // Publishes the outcome and reveals the zip; main thread only
        internal void FinishExport(PlaytestExportStatus packed, string zipPath)
        {
            if (packed is PlaytestExportFailed failed)
            {
                Fail(failed.Reason);
                return;
            }
            var done = (PlaytestExportDone)packed;
            var revealed = _reveal.Reveal(zipPath);
            Debug.Log($"[PlaytestReceiver] exported {done.BoxCount} box(es) to {zipPath}; {done.Missing.Count} item(s) missing");
            PlaytestExportStatusBoard.Set(new PlaytestExportDone(zipPath, done.BoxCount, done.Missing, revealed));
        }

        internal static void FailIfStillRunning()
        {
            if (PlaytestExportStatusBoard.Current.Value is PlaytestExportRunning) Fail("export ended unexpectedly; see Player.log");
        }

        private static void Fail(string reason)
        {
            Debug.LogWarning($"[PlaytestReceiver] export failed: {reason}");
            PlaytestExportStatusBoard.Set(new PlaytestExportFailed(reason));
        }
    }
}
