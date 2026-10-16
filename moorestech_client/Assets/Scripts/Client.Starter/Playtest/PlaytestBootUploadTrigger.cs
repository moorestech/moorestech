using Client.Game.InGame.BugReport.Submit;
using VContainer.Unity;

namespace Client.Starter.Playtest
{
    public sealed class PlaytestBootUploadTrigger : IStartable
    {
        private readonly IPlaytestUploadRequester _uploadRequester;
        public PlaytestBootUploadTrigger(IPlaytestUploadRequester uploadRequester) { _uploadRequester = uploadRequester; }
        public void Start()
        {
            // MainGame起動で溜まった箱を送る（Editorの直接再生にはタイトルの押し場が無い）
            // Send accumulated boxes on MainGame startup; direct Editor playback has no title push site
            _uploadRequester.RequestUpload();
        }
    }
}
