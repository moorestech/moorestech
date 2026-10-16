using System;
using System.IO;
using Client.Game.InGame.BugReport.Submit;
using VContainer.Unity;
using Client.Game.InGame.BugReport;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Client.PlaytestReceiver.Upload;

namespace Client.Starter.Playtest
{
    public sealed class PlaytestBootUploadTrigger : IStartable
    {
        private readonly IPlaytestUploadRequester _uploadRequester;
        private readonly PlaytestOutboxDirectories _directories;
        public PlaytestBootUploadTrigger(IPlaytestUploadRequester uploadRequester, PlaytestOutboxDirectories directories)
        { _uploadRequester = uploadRequester; _directories = directories; }
        public void Start()
        {
            // MainGame起動で溜まった箱を送る（Editorの直接再生にはタイトルの押し場が無い）
            // Send accumulated boxes on MainGame startup; direct Editor playback has no title push site
            CompleteAndRequestAsync().Forget();

            #region Internal

            async UniTaskVoid CompleteAndRequestAsync()
            {
#if UNITY_EDITOR
                var repositoryRoot = RepositoryStateProbe.RepositoryRoot;
                var masterDataRoot = RepositoryStateProbe.MasterDataRoot;
                await PlaytestOutboxAccess.Gate.WaitAsync(Application.exitCancellationToken);
                // 外部ディスクIOの箱補完失敗を隔離する
                // Isolate external disk IO failures while completing outbox evidence
                try
                {
                    // gitと資料IOだけをワーカーへ移し、Unity APIは先に取得する
                    // Move git and evidence IO to a worker after capturing Unity paths
                    await UniTask.RunOnThreadPool(() => PlaytestExistingReportEvidence.Complete(_directories, repositoryRoot, masterDataRoot));
                }
                // 外部ディスク列挙境界。配送要求へ進めて件数不明を画面にも表示する
                // External disk enumeration boundary: continue to delivery so unreadable inventory reaches the UI
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                    Debug.LogWarning($"[PlaytestReceiver] legacy outbox enumeration failed: {error.Message}");
                }
                finally { PlaytestOutboxAccess.Gate.Release(); }
                await UniTask.SwitchToMainThread();
#endif
                _uploadRequester.RequestUpload();
            }

            #endregion
        }
    }
}
