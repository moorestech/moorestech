using Client.Localization;
using System.Reflection;
using Client.Tests.Ui.State.Notification;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.Foundation.Stage.ScaleReading;
using Client.Ui.Screens.Notification;
using Client.Ui.State.Notification;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.Notification.NotificationTestContents;

namespace Client.Tests.Ui.Screens.Notification
{
    /// <summary>
    /// 通知が背面の層（舞台より前に描かれ、舞台と画面に隠れる層）の実画面の族に立つことを確かめる（Web の notificationLayering.test.ts 3 件と e2e layering.spec.ts 2 件）
    /// Verifies notifications stand in the viewport family of the behind-stage layer, drawn before and covered by the stage and screens (3 tests of Web notificationLayering.test.ts and 2 of e2e layering.spec.ts)
    /// </summary>
    public class NotificationLayeringTest : UiTestHost
    {
        private UiLayerStack _layers;
        private GameObject _gameObject;
        private ReactiveProperty<float> _stageScale;
        private NotificationLayerPresenter _presenter;

        [SetUp]
        public void BuildLayers()
        {
            // 文言の辞書を読んでおく（既存の部品テスト GamePanelTest・PanelActionButtonTest の SetUp と同じ）
            // Load the dictionaries first (same as the SetUp of the existing component tests GamePanelTest and PanelActionButtonTest)
            Localize.Initialize();
            _layers = UiLayerStack.Build(rootVisualElement);
            _gameObject = new GameObject("notification-layering");
        }

        [TearDown]
        public void DisposePresenter()
        {
            _presenter?.Dispose();
            _presenter = null;
            Object.DestroyImmediate(_gameObject);
        }

        /// <web-test file="src/features/notification/notificationLayering.test.ts">通知ホストは背面層のトークンを使い、最前面のトースト層を使わない</web-test>
        [Test]
        public void 通知ホストは背面の層を使いトーストの層を使わない()
        {
            Assert.AreEqual(UiLayer.BehindStage, NotificationLayerPresenter.HostLayer);
            Assert.AreNotEqual(UiLayer.Toast, NotificationLayerPresenter.HostLayer);
        }

        /// <web-test file="src/features/notification/notificationLayering.test.ts">通知はPortalの外、stageより前のDOM位置に描かれる</web-test>
        [Test]
        public void 通知の層はPortalの層と舞台の層より前に描かれる()
        {
            var notificationIndex = IndexOfLayer(NotificationLayerPresenter.HostLayer);
            foreach (var later in new[] { UiLayer.ViewportStage, UiLayer.Screen, UiLayer.WorldPin, UiLayer.Modal, UiLayer.Toast, UiLayer.Tutorial })
            {
                Assert.Less(notificationIndex, IndexOfLayer(later), later.ToString());
            }
        }

        /// <web-test file="src/features/notification/notificationLayering.test.ts">通知は実画面へ固定され、stage拡縮に追従しない</web-test>
        [Test]
        public void 通知は実画面の族へ置かれ縦の中央線に固定され左端は舞台の倍率に追従しない()
        {
            Assert.AreEqual(UiFamily.Viewport, NotificationLayerPresenter.HostFamily);
            var host = MountPresenterWithRow();
            Assert.AreEqual(Position.Absolute, host.resolvedStyle.position);
            Assert.AreEqual(rootVisualElement.layout.height * 0.5f, host.worldBound.y, 0.5f);

            // 舞台の倍率が 2 倍になっても、左端は実画面 16px のまま（倍率で割った 8 を当てて、拡縮後の見かけを 16px に保つ）
            // Even when the stage scale doubles the left edge stays 16 real px (8 is written so it looks 16 after scaling)
            _stageScale.Value = 2f;
            simulate.FrameUpdateMs(0);
            Assert.AreEqual(8f, host.style.left.value.value, 0.0001f);
            _stageScale.Value = 1f;
            simulate.FrameUpdateMs(0);
            Assert.AreEqual(16f, host.style.left.value.value, 0.0001f);
        }

        /// <web-test file="e2e/tests/notification/layering.spec.ts">インベントリを開いている間、通知はパネルの描画を一切変えない</web-test>
        [Test]
        public void 画面の層のパネルは通知の上に描かれ通知はその下のパネルの入力も奪わない()
        {
            var host = MountPresenterWithRow();
            var rowCenter = host[0].worldBound.center;
            var screenProbe = AddFullScreenProbe(UiLayer.Screen);
            var backdropProbe = AddFullScreenProbe(UiLayer.Backdrop);

            // 通知の行の上で拾われるのは前の層（画面）のパネル。その画面を外すと、通知は拾われず背後の層まで抜ける
            // Over the row the front (screen) panel is picked; with it removed the notification is skipped and the pick reaches the layer behind
            Assert.AreSame(screenProbe, rootVisualElement.panel.Pick(rowCenter));
            screenProbe.RemoveFromHierarchy();
            simulate.FrameUpdate();
            Assert.AreSame(backdropProbe, rootVisualElement.panel.Pick(rowCenter));
            Assert.AreEqual(PickingMode.Ignore, host.pickingMode);
            Assert.AreEqual(PickingMode.Ignore, host[0].pickingMode);
        }

        /// <web-test file="e2e/tests/notification/layering.spec.ts">通知ホストはstageより後ろの層に立つ</web-test>
        [Test]
        public void 通知ホストは舞台より後ろの層に立つ()
        {
            var host = MountPresenterWithRow();
            Assert.AreSame(_layers.GetLayer(UiLayer.BehindStage, UiFamily.Viewport), host.parent);
            Assert.Less(IndexOfLayer(UiLayer.BehindStage), IndexOfLayer(UiLayer.ViewportStage));
        }

        private VisualElement AddFullScreenProbe(UiLayer layer)
        {
            var probe = new VisualElement { name = $"{layer}-probe" };
            probe.style.position = Position.Absolute;
            probe.style.left = 0;
            probe.style.top = 0;
            probe.style.right = 0;
            probe.style.bottom = 0;
            _layers.GetLayer(layer, layer == UiLayer.Screen ? UiFamily.Stage : UiFamily.Viewport).Add(probe);
            simulate.FrameUpdate();
            return probe;
        }

        private NotificationHostView MountPresenterWithRow()
        {
            // 本番の Presenter をそのまま Initialize し、Model に積んだ通知が行として立つところまで通す
            // Initialize the production presenter as is and carry a notification pushed to the model up to a standing row
            var model = new NotificationModel();
            _stageScale = new ReactiveProperty<float>(1f);
            // SetUp で組んだ層と同じ層を GameUiRoot に持たせる（工場に任せると層がもう 1 組できて比べられない）
            // Give the GameUiRoot the same layers built in SetUp (the factory would build a second set that cannot be compared)
            var root = _gameObject.AddComponent<GameUiRoot>();
            typeof(GameUiRoot).GetField("_layers", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(root, _layers);
            _presenter = new NotificationLayerPresenter(root, model, model, new FakeNotificationTextLookup(), new FakeNotificationRowIcons(), new StageScaleSource(_stageScale));
            _presenter.Initialize();
            simulate.FrameUpdate();
            model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs(300);
            return rootVisualElement.Q<NotificationHostView>();
        }

        private int IndexOfLayer(UiLayer layer)
        {
            return rootVisualElement.IndexOf(_layers.GetLayer(layer, UiFamily.Viewport).parent);
        }
    }
}
