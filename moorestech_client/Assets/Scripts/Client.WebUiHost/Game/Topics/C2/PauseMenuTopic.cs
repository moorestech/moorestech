using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Ui.State.PauseMenu;
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
        private readonly PauseMenuModel _model;
        private readonly CompositeDisposable _subscriptions = new();

        public PauseMenuTopic(WebSocketHub hub, PauseMenuModel model)
        {
            _hub = hub;
            _model = model;

            // 変化のみ配信、snapshotで復元。源の束ねはモデルが持つ
            // Publishes only changes; restores via snapshot. The model owns combining the sources
            model.Current.Skip(1).Subscribe(_ => Publish()).AddTo(_subscriptions);
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
            var current = _model.Current.Value;
            return WebUiJson.Serialize(new PauseMenuDto
            {
                Delivery = PlaytestDeliveryDto.From(current.Delivery),
                Export = PlaytestExportDto.From(current.Export),
                Disconnected = current.Disconnected,
                Page = PauseMenuPageContract.ToContractText(current.Page),
                BugReport = new BugReportStatusDto
                {
                    Kind = current.Capture.Kind,
                    Missing = new List<string>(current.Capture.Missing),
                },
            });
        }
    }

    public class PauseMenuDto
    {
        public PlaytestDeliveryDto Delivery;
        public PlaytestExportDto Export;
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
