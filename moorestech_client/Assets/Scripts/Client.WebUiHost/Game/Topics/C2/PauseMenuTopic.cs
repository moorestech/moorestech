using System;
using System.Collections.Generic;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.Presenter.PauseMenu;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    public class PauseMenuTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "pause_menu.current";

        private readonly WebSocketHub _hub;
        private readonly NetworkDisconnectState _state;
        private readonly BugReportCaptureSession _bugReportCaptureSession;
        private readonly PauseMenuStateService _pauseMenuStateService;
        private readonly CompositeDisposable _subscriptions = new();

        public PauseMenuTopic(WebSocketHub hub, NetworkDisconnectState state, BugReportCaptureSession bugReportCaptureSession, PauseMenuStateService pauseMenuStateService)
        {
            _hub = hub;
            _state = state;
            _bugReportCaptureSession = bugReportCaptureSession;
            _pauseMenuStateService = pauseMenuStateService;

            // 変化のみ配信、snapshotで復元
            // Publishes only changes; restores via snapshot
            state.OnDisconnectedChanged.Skip(1).Subscribe(_ => Publish()).AddTo(_subscriptions);
            bugReportCaptureSession.Status.Skip(1).Subscribe(_ => Publish()).AddTo(_subscriptions);
            pauseMenuStateService.CurrentPage.Skip(1).Subscribe(_ => Publish()).AddTo(_subscriptions);
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        private void Publish()
        {
            _hub.Publish(TopicName, BuildJson());
        }

        private string BuildJson()
        {
            var status = _bugReportCaptureSession.Status.Value;
            return WebUiJson.Serialize(new PauseMenuDto
            {
                Disconnected = _state.IsDisconnected,
                Page = PauseMenuPageContract.ToContractText(_pauseMenuStateService.CurrentPage.Value),
                BugReport = new BugReportStatusDto
                {
                    Kind = status.Kind,
                    Missing = new List<string>(status.Missing),
                },
            });
        }
    }

    public class PauseMenuDto
    {
        public bool Disconnected;
        public BugReportStatusDto BugReport;
        public string Page;
    }

    // 送信可否の結論はC#の判定式が出した種別をそのまま載せる。Web側で組み立て直させない
    // The send-permission verdict travels as the kind C#'s rule produced, so the Web never rebuilds it
    public class BugReportStatusDto
    {
        public string Kind;
        public List<string> Missing;
    }
}
