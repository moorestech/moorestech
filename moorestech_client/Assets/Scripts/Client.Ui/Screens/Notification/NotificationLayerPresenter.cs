using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.Foundation.Stage.ScaleReading;
using Client.Ui.State.Notification;
using Client.Ui.State.Notification.Text;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.Notification
{
    /// <summary>
    /// 背面の層（舞台の裏）の実画面の族へ通知のホストを常駐させ、読み口の行の一覧を描かせ、書き口をホストへ渡す（Web App.tsx の stage より前の NotificationHost）
    /// Keeps the notification host resident in the viewport family of the behind-stage layer, renders the read side's rows and hands the write side to the host (Web App.tsx NotificationHost before the stage)
    /// </summary>
    public sealed class NotificationLayerPresenter : IInitializable
    {
        internal const UiLayer HostLayer = UiLayer.BehindStage;
        internal const UiFamily HostFamily = UiFamily.Viewport;

        private readonly GameUiRoot _gameUiRoot;
        private readonly NotificationModel _model;
        private readonly INotificationTextLookup _textLookup;
        private readonly INotificationRowIcons _icons;
        private readonly StageScaleSource _stageScaleSource;
        private NotificationHostView _host;

        public NotificationLayerPresenter(GameUiRoot gameUiRoot, NotificationModel model, INotificationTextLookup textLookup, INotificationRowIcons icons, StageScaleSource stageScaleSource)
        {
            _gameUiRoot = gameUiRoot;
            _model = model;
            _textLookup = textLookup;
            _icons = icons;
            _stageScaleSource = stageScaleSource;
        }

        public void Initialize()
        {
            // 中身は PanelSettings の倍率で舞台と同率、倍率の外の左端だけ StageScaleSource を購読して実画面 16px に保つ（Web の left: 1rem＋中身の scale(var(--ui-scale))）
            // Contents scale with the stage via PanelSettings; only the unscaled left edge follows StageScaleSource to stay 16 real px (Web left: 1rem + contents scale(var(--ui-scale)))
            _host = new NotificationHostView(_model, _textLookup, _icons);
            _gameUiRoot.GetLayer(HostLayer, HostFamily).Add(_host);
            _stageScaleSource.Scale.Subscribe(_host.SetStageScale);
            _model.Current.Subscribe(_host.Render);
        }

    }
}
