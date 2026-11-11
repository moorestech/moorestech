using Client.Tests.Ui.Components;
using Client.Ui.Screens.Notification;
using Client.Ui.State.Notification;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.Notification.NotificationTestContents;

namespace Client.Tests.Ui.Screens.Notification
{
    /// <summary>
    /// 通知の 1 行の色・アイコンの枠・寸法を確かめる（L1。UITK だけのテスト）
    /// Verifies the color, icon slot and size of one notification row (L1; UITK-only tests)
    /// </summary>
    public class NotificationRowViewTest : ScreenTestHost
    {
        private readonly FakeNotificationRowIcons _icons = new();

        [Test]
        public void 拒否の行は警告色で出る()
        {
            var row = MountShown(new NotificationRow(1, 0, Denied("denied.craftResultFull", NoParams, NoIcon)), UiText.FromVerbatim("full"));
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--text-insufficient"), row.Q<Label>(className: "notification-row__text").resolvedStyle.color, "denied text");

            row.Show(new NotificationRow(2, 0, Achievement("achievement.unlockedBlock", NoParams, NoIcon)), UiText.FromVerbatim("ok"), _icons);
            simulate.FrameUpdate();
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--text-high-contrast"), row.Q<Label>(className: "notification-row__text").resolvedStyle.color, "normal text");
        }

        [Test]
        public void アイコンの無い行はアイコンの枠を出さずある行は代わりの文字を出す()
        {
            var row = MountShown(new NotificationRow(1, 0, Achievement("achievement.unlockedBlock", NoParams, NoIcon)), UiText.FromVerbatim("no icon"));
            var iconSlot = row.Q(className: "notification-row__icon");
            Assert.IsFalse(IsShown(iconSlot));

            row.Show(new NotificationRow(2, 0, Earned(2, 5, "itemEarned.mined")), UiText.FromVerbatim("Stone +5"), _icons);
            Assert.IsTrue(IsShown(iconSlot));
            StringAssert.Contains("#2", ConcatText(iconSlot));
            Assert.AreEqual(22f, iconSlot.resolvedStyle.width, 0.5f);
        }

        [Test]
        public void 文字だけの行は幅がトークンで高さがWebと同じ30px()
        {
            var row = MountShown(new NotificationRow(1, 0, Achievement("achievement.unlockedBlock", NoParams, NoIcon)), UiText.FromVerbatim("Block unlocked"));

            // Web: 15px×1.2=18px の行＋上下 5px の padding＋上下 1px の枠
            // Web: an 18px line (15px x 1.2) + 5px vertical padding + 1px vertical border
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--notification-width"), row.resolvedStyle.width, 1f);
            Assert.AreEqual(30f, row.layout.height, 1f);
        }

        // ホストと同じく文を入れてからパネルに載せる（文の無いラベルが付くと LocalizedTextBinding が LogError を出す）
        // Show the text before attaching, as the host does (a label attached without text makes LocalizedTextBinding log an error)
        private NotificationRowView MountShown(NotificationRow row, UiText text)
        {
            var view = new NotificationRowView();
            view.Show(row, text, _icons);
            return Mount(view);
        }

        private static string ConcatText(VisualElement root)
        {
            var text = string.Empty;
            root.Query<TextElement>().ForEach(element => text += element.text);
            return text;
        }
    }
}
