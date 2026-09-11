using System;
using System.Collections.Generic;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.Presenter.PauseMenu;
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
        private readonly CompositeDisposable _subscriptions = new();

        public PauseMenuTopic(WebSocketHub hub, NetworkDisconnectState state, BugReportCaptureSession bugReportCaptureSession)
        {
            _hub = hub;
            _state = state;
            _bugReportCaptureSession = bugReportCaptureSession;

            // 切断状態と確保状態の変化だけを配信し、再接続時はsnapshotから復元する
            // Publish only disconnect and capture-status changes; restore from the snapshot after reconnect
            state.OnDisconnectedChanged.Skip(1).Subscribe(_ => Publish()).AddTo(_subscriptions);
            bugReportCaptureSession.Status.Skip(1).Subscribe(_ => Publish()).AddTo(_subscriptions);
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
                BugReport = new BugReportStatusDto
                {
                    HasSession = status.HasSession,
                    CapturePending = status.CapturePending,
                    Missing = new List<string>(status.Missing),
                },
            });
        }
    }

    public class PauseMenuDto
    {
        public bool Disconnected;
        public BugReportStatusDto BugReport;
    }

    public class BugReportStatusDto
    {
        public bool HasSession;
        public bool CapturePending;
        public List<string> Missing;
    }
}
