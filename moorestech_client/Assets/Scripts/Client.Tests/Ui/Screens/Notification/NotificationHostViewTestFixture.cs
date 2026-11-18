using System;
using Client.Tests.Ui.State.Notification;
using Client.Ui.Foundation.Stage.ScaleReading;
using Client.Ui.Screens.Notification;
using Client.Ui.State.Notification;
using NUnit.Framework;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Notification
{
    /// <summary>
    /// 通知のホストを載せ、本番の Presenter と同じく読み口の変化を Render へ流し、書き口を View に渡す L1 の土台
    /// L1 base that mounts the notification host, pipes read-side changes into Render and hands the write side to the View like the production presenter
    /// </summary>
    public abstract class NotificationHostViewTestFixture : ScreenTestHost
    {
        private IDisposable _subscription;

        protected NotificationModel Model { get; private set; }
        protected FakeNotificationTextLookup TextLookup { get; private set; }
        protected NotificationHostView Host { get; private set; }
        protected ReactiveProperty<float> StageScaleForTest { get; private set; }
        private IDisposable _scaleSubscription;

        [SetUp]
        public void MountNotificationHost()
        {
            Model = new NotificationModel();
            TextLookup = new FakeNotificationTextLookup();
            // 本番の Presenter と同じく StageScaleSource の倍率をホストへ流す。倍率はテストが外から与える
            // Feed the StageScaleSource scale into the host like the production presenter; the test supplies the scale
            StageScaleForTest = new ReactiveProperty<float>(1f);
            var stageScaleSource = new StageScaleSource(StageScaleForTest);
            Host = Mount(new NotificationHostView(Model, TextLookup, new FakeNotificationRowIcons()));
            _scaleSubscription = stageScaleSource.Scale.Subscribe(Host.SetStageScale);
            _subscription = Model.Current.Subscribe(Host.Render);
        }

        [TearDown]
        public void DisposeHostSubscription()
        {
            _subscription.Dispose();
            _scaleSubscription.Dispose();
        }

        // パネルの時計を進める（UiMotion はパネルのスケジューラで進む。1 回が 1 フレーム）
        // Advance the panel clock (UiMotion runs on the panel scheduler; one call is one frame)
        protected void AdvancePanel(int milliseconds)
        {
            simulate.FrameUpdateMs(milliseconds);
        }

        protected NotificationRowView RowAt(int index)
        {
            return (NotificationRowView)Host[index];
        }

        protected static string TextOf(VisualElement root)
        {
            var text = string.Empty;
            root.Query<TextElement>().ForEach(element => text += element.text);
            return text;
        }
    }
}
