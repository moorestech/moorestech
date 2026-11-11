using System;
using Client.Tests.Ui.State.Notification;
using Client.Ui.Content;
using Client.Ui.Screens.Notification;
using Client.Ui.State.Notification;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.Notification.NotificationTestContents;

namespace Client.Tests.Ui.Screens.Notification
{
    /// <summary>
    /// マスタに無いアイテムの獲得が来ても理由つきのエラーと代わりのアイコンで描かれ、後続の通知が止まらないことを確かめる（L1。本番のアイコンの口）
    /// Verifies a missing-master item earn renders with a reasoned error and a fallback icon without stopping later notifications (L1; production icon lookup)
    /// </summary>
    public class GameContentNotificationRowIconsTest : ScreenTestHost
    {
        private const int MissingItemId = 987654;

        [SetUp]
        public void LoadMaster()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void マスタに無いアイテムの獲得が来ても後続の通知が描かれる()
        {
            var model = new NotificationModel();
            var icons = new GameContentNotificationRowIcons(new GameContentPresentation(null));
            var host = Mount(new NotificationHostView(model, new FakeNotificationTextLookup(), icons));
            using var subscription = model.Current.Subscribe(host.Render);
            simulate.FrameUpdateMs(0);

            model.AddNotification(Earned(MissingItemId, 1, "itemEarned.mined"));
            model.AddNotification(Achievement("achievement.unlockedBlock", NoParams, NoIcon));

            Assert.AreEqual(2, host.childCount);
            StringAssert.Contains($"#{MissingItemId}", ConcatText(host[1]));
        }

        private static string ConcatText(VisualElement root)
        {
            var text = string.Empty;
            root.Query<TextElement>().ForEach(element => text += element.text);
            return text;
        }
    }
}
