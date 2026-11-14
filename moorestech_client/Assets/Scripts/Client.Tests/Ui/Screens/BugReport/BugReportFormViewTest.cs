using System.Linq;
using Client.Game.InGame.BugReport.Capture;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BugReport
{
    /// <summary>
    /// 記述欄・送信可否・状態行・送信中ロック（L1）
    /// Description, send availability, status line, submit lock (L1)
    /// </summary>
    public class BugReportFormViewTest : BugReportFormScreenTestHost
    {

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/inputAndAvailability.test.ts">記述欄と送信ボタンを描く</web-test>
        /// <web-test file="src/features/pauseMenu/BugReportFormTests/inputAndAvailability.test.ts">空文字では送信ボタンを無効にして送らない</web-test>
        /// <web-test file="src/features/pauseMenu/BugReportFormTests/inputAndAvailability.test.ts">空白だけの記述も無効として扱う</web-test>
        [Test]
        public void 記述欄と送信ボタンを描き空文字と空白だけでは送れない()
        {
            Assert.IsTrue(Description().multiline);
            Assert.AreEqual("Send bug report", SendButton().Q<Label>().text);
            Assert.IsFalse(SendButton().enabledSelf);
            Type("   \n ");
            Assert.IsFalse(SendButton().enabledSelf);
            Type("broken belt");
            Assert.IsTrue(SendButton().enabledSelf);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/inputAndAvailability.test.ts">確保中は送信ボタンを押せない</web-test>
        /// <web-test file="src/features/pauseMenu/BugReportFormTests/inputAndAvailability.test.ts">状態行は1本だけ描き、確保中は確保中の文言を出す</web-test>
        [Test]
        public void 確保中は送信ボタンを押せず状態行は確保中の1本だけ()
        {
            Type("broken belt");
            View.Apply(Model(BugReportCaptureStatus.Capturing, "video"));
            Assert.IsFalse(SendButton().enabledSelf);
            Assert.AreEqual(1, View.Query<Label>("bug-report-status").ToList().Count);
            Assert.AreEqual(new UiText(LocalizationKeys.Ui.BugReport.CapturePending).Resolve(), Status().text);
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/inputAndAvailability.test.ts">確保セッションが無いときは押せず理由を出す</web-test>
        [Test]
        public void 確保セッションが無いときは押せず理由を出す()
        {
            Type("broken belt");
            View.Apply(Model(BugReportCaptureStatus.NoSession));
            Assert.IsFalse(SendButton().enabledSelf);
            Assert.IsNotNull(Status());
        }

        /// <web-test file="src/features/pauseMenu/BugReportFormTests/inputAndAvailability.test.ts">送れる状態なら欠損の一覧を同じ1本の行で出す</web-test>
        [Test]
        public void 送れる状態なら欠損の一覧を同じ1本の行で出す()
        {
            Assert.IsNull(View.Q<Label>("bug-report-status"));
            View.Apply(Model(BugReportCaptureStatus.Ready, "video", "logs"));
            StringAssert.Contains("video, logs", Status().text);
        }

        [Test]
        public void 応答待ちが終わってもホスト送信中は種別と本文を保護する()
        {
            simulate.Click(Find<Button>(View, "bug-report-kind-feedback"));
            Type("nice");
            View.Apply(Model(BugReportCaptureStatus.Submitting));
            Assert.IsFalse(Description().enabledSelf);
            Assert.IsFalse(Find<Button>(View, "bug-report-kind-bug").enabledSelf);
            Assert.IsFalse(SendButton().enabledSelf);
            StringAssert.Contains("impressions", Status().text);
        }

        [Test]
        public void 見出しと記述欄と状態行とボタンの縦の間はMantineのspacing_mdの16px()
        {
            // 未解決の var() は UITK が黙って 0 にするので、4 か所が --mantine-spacing-md の 16px に解決されることを見る
            // UITK silently turns an unresolved var() into 0, so check all four resolve to the 16px of --mantine-spacing-md
            const float mantineSpacingMd = 16f;
            View.Apply(Model(BugReportCaptureStatus.Ready, "video"));
            simulate.FrameUpdate();
            Assert.AreEqual(mantineSpacingMd, View.Q<Label>(className: "bug-report__field-label").resolvedStyle.marginBottom, 0.01f);
            Assert.AreEqual(mantineSpacingMd, Description().parent.resolvedStyle.marginTop, 0.01f);
            Assert.AreEqual(mantineSpacingMd, Status().resolvedStyle.marginTop, 0.01f);
            Assert.AreEqual(mantineSpacingMd, SendButton().resolvedStyle.marginTop, 0.01f);
        }
    }
}
