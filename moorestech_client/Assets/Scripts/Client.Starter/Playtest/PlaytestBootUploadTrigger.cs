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
                try
                {
                    // gitと資料IOだけをワーカーへ移し、Unity APIは先に取得する
                    // Move git and evidence IO to a worker after capturing Unity paths
                    await UniTask.RunOnThreadPool(() => PlaytestExistingReportEvidence.Complete(_directories, repositoryRoot, masterDataRoot));
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
