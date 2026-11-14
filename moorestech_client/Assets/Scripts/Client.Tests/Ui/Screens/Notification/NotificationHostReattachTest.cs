using System.Text.RegularExpressions;
using Client.Tests.Ui.State.Notification;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.Notification.NotificationTestContents;

namespace Client.Tests.Ui.Screens.Notification
{
    /// <summary>
    /// 外して戻した通知行は消えず生存を頭から回し直して退場の終わりで除かれ、外れている間の新着も戻すと出る（L1。DC-24）。トークンが読めない理由のログ（DC-28）も確かめる
    /// A row whose host was detached and re-attached stays, restarts its lifetime from the head and leaves at the exit end, and a notification that arrived while detached shows on re-attach (L1; DC-24); also checks the unreadable-token log (DC-28)
    /// </summary>
    public class NotificationHostReattachTest : NotificationHostViewTestFixture
    {
        private const string ProbePath = "Assets/Scripts/Client.Tests/Ui/Components/Controls/ControlKnobOverrides.uss";
        private static readonly Regex DetachedWarning = new(@"^\[NotificationHostView\] detached with \d+ rows");

        [Test]
        public void 入場中と表示中に外して戻した行は残り生存を回し直して退場の終わりで消える()
        {
            Model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(100);
            var row = RowAt(0);
            Assert.That(row.resolvedStyle.opacity, Is.GreaterThan(0f));

            // 入場中に外して戻しても Model の行は残り、入場の頭（透明）から回し直す
            // Detaching and re-attaching mid-enter keeps the model row and replays from the enter head (transparent)
            var parent = Host.parent;
            DetachAndReattach(parent);
            Assert.AreEqual(1, Model.Current.Value.Rows.Count);
            Assert.AreEqual(1, Host.childCount);
            Assert.That(row.resolvedStyle.opacity, Is.LessThan(0.5f));
            // 表示中まで進めてもう一度外して戻すと、また頭から回る
            // Advance into the shown phase, detach and re-attach again, and it replays from the head again
            AdvancePanel(3000);
            DetachAndReattach(parent);
            Assert.That(row.resolvedStyle.opacity, Is.LessThan(0.5f));

            // 回し直した生存は頭から数えて退場の終わりで書き口から除かれる
            // The restarted lifetime ends at the exit end counted from the head, and the row leaves through the write side
            // 続きから再開なら残り約 4000ms で消えるので、5000ms 後もまだ 1 行あることが「頭から数えて」を時間で固定する
            // Resuming would remove it after about 4000ms left, so still one row after 5000ms pins "counted from the head" in time
            AdvancePanel(5000);
            Assert.AreEqual(1, Model.Current.Value.Rows.Count);
            AdvancePanel(5000);
            Assert.AreEqual(0, Model.Current.Value.Rows.Count);
            Assert.AreEqual(0, Host.childCount);
        }

        [Test]
        public void 外れている間に届いた通知は戻したときに表示される()
        {
            var parent = Host.parent;
            // 行が無いうちに外すので外れた警告は出ない（警告は行を抱えて外れたときだけ）
            // Detached with no rows, so no detach warning (it only fires when detaching with rows)
            Host.RemoveFromHierarchy();
            AdvancePanel(0);
            // 外れ中の通知は保留理由を警告する
            // Detached notification warns why it is held
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[NotificationHostView\] notification \d+ held while detached"));
            Model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            AdvancePanel(0);
            Assert.AreEqual(1, Model.Current.Value.Rows.Count);

            parent.Add(Host);
            AdvancePanel(0);
            AdvancePanel(3000);
            Assert.AreEqual(1, Model.Current.Value.Rows.Count);
            Assert.AreEqual(1, Host.childCount);
            Assert.That(RowAt(0).resolvedStyle.opacity, Is.GreaterThan(0f));
        }

        [Test]
        public void トークンが読めないと読めない理由を1回LogErrorする()
        {
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ProbePath);
            Assert.IsNotNull(sheet, ProbePath);
            LogAssert.Expect(LogType.Error, new Regex(@"\[UssCustomStyleReader\] --notification-host-enter-duration"));
            LogAssert.Expect(LogType.Error, new Regex(@"^\[NotificationHostView\] motion tokens are unreadable; showing without motion"));
            Host.styleSheets.Add(sheet);
            Host.AddToClassList("notification-host-unreadable-tokens-probe");
            AdvancePanel(0);
            AdvancePanel(0);
        }

        private void DetachAndReattach(VisualElement parent)
        {
            // 外すと警告、戻して 1 フレームで再生
            // Detach warns; one frame after re-attach restarts
            LogAssert.Expect(LogType.Warning, DetachedWarning);
            Host.RemoveFromHierarchy();
            AdvancePanel(0);
            parent.Add(Host);
            AdvancePanel(0);
        }
    }
}
