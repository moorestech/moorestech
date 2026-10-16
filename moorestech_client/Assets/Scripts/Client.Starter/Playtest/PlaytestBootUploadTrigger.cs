using Client.Game.InGame.BugReport.Submit;
using VContainer.Unity;
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
#if UNITY_EDITOR
            PlaytestExistingReportEvidence.Complete(_directories);
#endif
            _uploadRequester.RequestUpload();
        }
    }
}
