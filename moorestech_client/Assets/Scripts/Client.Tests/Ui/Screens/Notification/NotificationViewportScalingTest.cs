using Client.Tests.Ui.Components;
using NUnit.Framework;
using UnityEngine;
using static Client.Tests.Ui.State.Notification.NotificationTestContents;

namespace Client.Tests.Ui.Screens.Notification
{
    /// <summary>
    /// Web の portalLayerScaling.spec.ts の通知の 1 件を 2 つの論理解像度で写す。2560x1080 は Expand（参照 1280x720）で 1706.7x720 になる
    /// Copies the notification test of Web portalLayerScaling.spec.ts at two logical sizes; 2560x1080 becomes 1706.7x720 under Expand (reference 1280x720)
    /// </summary>
    [TestFixture(1280f, 720f)]
    [TestFixture(1706.7f, 720f)]
    public class NotificationViewportScalingTest : NotificationHostViewTestFixture
    {
        private readonly Vector2 _logicalSize;

        public NotificationViewportScalingTest(float width, float height)
        {
            _logicalSize = new Vector2(width, height);
        }

        [OneTimeSetUp]
        public void UseLogicalSize()
        {
            // 基底の OneTimeSetUp（本番の 1280x720）の後に走り、パネルの寸法だけを差し替える
            // Runs after the base OneTimeSetUp (production 1280x720) and only swaps the panel size
            panelSize = _logicalSize;
        }

        /// <web-test file="e2e/tests/system/portalLayerScaling.spec.ts">通知の幅はstage比のまま解像度に依らず、高解像度でも画面幅の2割に収まる</web-test>
        [Test]
        public void 通知の幅は解像度に依らず舞台のままで画面幅の2割に収まる()
        {
            Model.AddNotification(Earned(2, 1, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(300);

            // 行の幅は舞台 px の固定値のまま、画面の論理幅に対しては 2 割以内
            // Row width stays the fixed stage px value and within 20% of the logical screen width
            var rowWidth = RowAt(0).worldBound.width;
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--notification-width"), rowWidth, 1f);
            Assert.LessOrEqual(rowWidth / rootVisualElement.worldBound.width, 0.2f + 0.002f);
        }
    }
}
