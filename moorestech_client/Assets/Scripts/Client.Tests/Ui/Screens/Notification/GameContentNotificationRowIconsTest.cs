using System;
using System.Text.RegularExpressions;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.State.Notification;
using Client.Ui.Content;
using Client.Ui.Screens.Notification;
using Client.Ui.State.Notification;
using Core.Master;
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
    /// マスタに無いアイテムの獲得・実績が来ても理由つきのエラーと代わりのアイコンで描かれ、後続の通知が止まらないことを確かめる（L1。本番のアイコンの口）
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
            LogAssert.Expect(LogType.Error, new Regex($"item {MissingItemId} is not in the item master"));
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

        [Test]
        public void マスタに無いアイテムのアイコンが付いた実績でも理由つきのエラーが出て後続が描かれる()
        {
            LogAssert.Expect(LogType.Error, new Regex($"item {MissingItemId} is not in the item master"));
            var model = new NotificationModel();
            var icons = new GameContentNotificationRowIcons(new GameContentPresentation(null));
            var host = Mount(new NotificationHostView(model, new FakeNotificationTextLookup(), icons));
            using var subscription = model.Current.Subscribe(host.Render);
            simulate.FrameUpdateMs(0);

            model.AddNotification(Achievement("achievement.unlockedItem", NoParams, ItemIcon(MissingItemId)));
            model.AddNotification(Achievement("achievement.unlockedBlock", NoParams, NoIcon));

            Assert.AreEqual(2, host.childCount);
            StringAssert.Contains($"#{MissingItemId}", ConcatText(host[1]));
        }

        [Test]
        public void TryItemはマスタに無いIDでfalseとnullを返しあるIDでItemと同じ絵を返す()
        {
            var texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            var content = new GameContentPresentation(new FakeContentTextureSource(texture, null, null));
            var knownId = MasterHolder.ItemMaster.GetItemId(new Guid("00000000-0000-0000-1234-000000000001"));

            Assert.IsFalse(content.TryItem(new ItemId(99999), out var missingIcon));
            Assert.IsNull(missingIcon);
            Assert.IsTrue(content.TryItem(knownId, out var icon));
            Assert.AreSame(content.Item(knownId).Texture, icon.Texture);
            Assert.AreEqual(content.Item(knownId).IdText, icon.IdText);
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static string ConcatText(VisualElement root)
        {
            var text = string.Empty;
            root.Query<TextElement>().ForEach(element => text += element.text);
            return text;
        }
    }
}
