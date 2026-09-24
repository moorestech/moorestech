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

            // 切断状態・確保状態・今の画面の変化だけを配信し、再接続時はsnapshotから復元する
            // Publish only disconnect, capture-status and page changes; restore from the snapshot after reconnect
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

    // 画面名の契約文字列。Webとの変換はここ1か所で行う（前例 PlaytestReportKindText）
    // Contract text for page names; conversion to and from the Web happens only here (precedent: PlaytestReportKindText)
    public static class PauseMenuPageContract
    {
        public static string ToContractText(PauseMenuPage page)
        {
            return page switch
            {
                PauseMenuPage.Top => "top",
                PauseMenuPage.Settings => "settings",
                PauseMenuPage.BugReport => "bugReport",
                _ => throw new ArgumentOutOfRangeException(nameof(page), page, null),
            };
        }

        public static bool TryParse(string text, out PauseMenuPage page)
        {
            switch (text)
            {
                case "top": page = PauseMenuPage.Top; return true;
                case "settings": page = PauseMenuPage.Settings; return true;
                case "bugReport": page = PauseMenuPage.BugReport; return true;
                default: page = PauseMenuPage.Top; return false;
            }
        }
    }
}
