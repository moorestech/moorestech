using Client.Localization;
using Client.Tests.Ui.State.Notification;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Notification;
using Client.Ui.State.Notification;
using NUnit.Framework;
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

        [SetUp]
        public void BuildLayers()
        {
            // 文言の辞書を読んでおく（既存の部品テスト GamePanelTest・PanelActionButtonTest の SetUp と同じ）
            // Load the dictionaries first (same as the SetUp of the existing component tests GamePanelTest and PanelActionButtonTest)
            Localize.Initialize();
            _layers = UiLayerStack.Build(rootVisualElement);
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
        public void 通知は実画面の族へ置かれ縦の中央線に固定される()
        {
            Assert.AreEqual(UiFamily.Viewport, NotificationLayerPresenter.HostFamily);
            var host = MountHostWithRow();
            Assert.AreEqual(Position.Absolute, host.resolvedStyle.position);
            Assert.AreEqual(rootVisualElement.layout.height * 0.5f, host.worldBound.y, 0.5f);
        }

        /// <web-test file="e2e/tests/notification/layering.spec.ts">インベントリを開いている間、通知はパネルの描画を一切変えない</web-test>
        [Test]
        public void 画面の層のパネルは通知の上に描かれ通知はその入力も奪わない()
        {
            var host = MountHostWithRow();
            var panelProbe = new VisualElement { name = "screen-panel-probe" };
            panelProbe.style.position = Position.Absolute;
            panelProbe.style.left = 0;
            panelProbe.style.top = 0;
            panelProbe.style.right = 0;
            panelProbe.style.bottom = 0;
            _layers.GetLayer(UiLayer.Screen, UiFamily.Stage).Add(panelProbe);
            simulate.FrameUpdate();

            // 画面の層は後に描かれ、通知の行の上で拾われるのは画面のパネル
            // The screen layer draws later, and the screen panel is what gets picked over the notification row
            Assert.Less(IndexOfLayer(UiLayer.BehindStage), IndexOfLayer(UiLayer.Screen));
            Assert.AreSame(panelProbe, rootVisualElement.panel.Pick(host[0].worldBound.center));
        }

        /// <web-test file="e2e/tests/notification/layering.spec.ts">通知ホストはstageより後ろの層に立つ</web-test>
        [Test]
        public void 通知ホストは舞台より後ろの層に立つ()
        {
            var host = MountHostWithRow();
            var hostLayerContainer = _layers.GetLayer(UiLayer.BehindStage, UiFamily.Viewport).parent;
            Assert.IsTrue(hostLayerContainer.Contains(host));
            Assert.Less(IndexOfLayer(UiLayer.BehindStage), IndexOfLayer(UiLayer.ViewportStage));
        }

        private NotificationHostView MountHostWithRow()
        {
            var model = new NotificationModel();
            var host = new NotificationHostView(model, new FakeNotificationTextLookup(), new FakeNotificationRowIcons());
            _layers.GetLayer(NotificationLayerPresenter.HostLayer, NotificationLayerPresenter.HostFamily).Add(host);
            simulate.FrameUpdate();
            model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            host.Render(model.Current.Value);
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs(300);
            return host;
        }

        private int IndexOfLayer(UiLayer layer)
        {
            return rootVisualElement.IndexOf(_layers.GetLayer(layer, UiFamily.Viewport).parent);
        }
    }
}
