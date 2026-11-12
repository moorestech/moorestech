using Client.Tests.Ui.State.Notification;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.Notification.NotificationTestContents;

namespace Client.Tests.Ui.Screens.Notification
{
    /// <summary>
    /// 外して戻した通知行は再生し直さず、生存の最終の姿に揃って書き口から除かれる（L1。DC-24）
    /// A row whose host was detached and re-attached is not replayed; it settles to the lifetime's final pose and is removed through the write side (L1; DC-24)
    /// </summary>
    public class NotificationHostReattachTest : NotificationHostViewTestFixture
    {
        [Test]
        public void 外して戻した通知行は最終の見た目に揃い書き口から除かれる()
        {
            Model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(500);
            var row = RowAt(0);
            Assert.That(row.resolvedStyle.opacity, Is.GreaterThan(0f));

            // 途中で外すと動きが止まる（開発者への警告は出る）
            // Detaching mid-motion stops it (the developer warning is raised)
            var parent = Host.parent;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"^\[NotificationHostView\] detached with 1 rows"));
            Host.RemoveFromHierarchy();
            AdvancePanel(0);

            // 戻すと再生し直さず、退場の終わりの姿（透明）で Model から除かれる
            // Re-attaching does not replay; the row takes the exit-end pose (transparent) and leaves the model
            parent.Add(Host);
            AdvancePanel(0);
            Assert.AreEqual(0f, row.style.opacity.value, 0.0001f);
            Assert.AreEqual(0, Model.Current.Value.Rows.Count);
            Assert.AreEqual(0, Host.childCount);
        }
    }
}
