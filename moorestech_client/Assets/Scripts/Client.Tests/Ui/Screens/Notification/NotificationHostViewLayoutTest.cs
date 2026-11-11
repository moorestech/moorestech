using System;
using Client.Localization;
using Client.Tests.Ui.Components;
using Client.Tests.Ui.State.Notification;
using Client.Ui.Screens.Notification;
using Client.Ui.State.Notification;
using Client.Ui.State.Notification.Content;
using Core.Master;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.Notification.NotificationTestContents;

namespace Client.Tests.Ui.Screens.Notification
{
    /// <summary>
    /// 通知のホストの並び・寸法・文・入力・言語を確かめる（L1。Web の e2e fixedWidth・itemEarned・layering の 1 件と UITK だけのテスト）
    /// Verifies the host's order, size, text, input and language (L1; Web e2e fixedWidth, itemEarned, one layering test and UITK-only tests)
    /// </summary>
    public class NotificationHostViewLayoutTest : NotificationHostViewTestFixture
    {
        /// <web-test file="e2e/tests/notification/fixedWidth.spec.ts">長さの違う通知が同時に出ても各行の横幅は固定値のまま</web-test>
        [Test]
        public void 長さの違う通知が同時に出ても各行の横幅は固定値のまま()
        {
            TextLookup.SetItemName(new ItemId(7), "Ore");
            TextLookup.SetItemName(new ItemId(9), "An extremely long item name that has to wrap across several lines of the row");
            Model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            Model.AddNotification(Earned(9, 1, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(300);
            var width = ThemeTokenFile.ResolvePixels("--notification-width");
            Assert.AreEqual(width, RowAt(0).layout.width, 1f);
            Assert.AreEqual(width, RowAt(1).layout.width, 1f);
            Assert.Greater(RowAt(0).layout.height, RowAt(1).layout.height);
        }

        /// <web-test file="e2e/tests/notification/itemEarned.spec.ts">アイテム獲得通知はアイコンとアイテム名と獲得数を出し連続獲得で数値が伸びる</web-test>
        [Test]
        public void アイテム獲得通知はアイコンとアイテム名と獲得数を出し連続獲得で数値が伸びる()
        {
            TextLookup.SetItemName(new ItemId(2), "Stone");
            Model.AddNotification(Earned(2, 5, "itemEarned.mined"));
            AdvancePanel(0);
            StringAssert.Contains("#2", TextOf(RowAt(0).Q(className: "notification-row__icon")));
            StringAssert.EndsWith("Stone +5", RowAt(0).Q<Label>(className: "notification-row__text").text);

            Model.AddNotification(Earned(2, 3, "itemEarned.mined"));
            AdvancePanel(0);
            Assert.AreEqual(1, Host.childCount);
            StringAssert.EndsWith("Stone +8", RowAt(0).Q<Label>(className: "notification-row__text").text);
        }

        /// <web-test file="e2e/tests/notification/layering.spec.ts">GameScreenでも通知はマウントされ続け、矩形を持つ</web-test>
        [Test]
        public void ホストは常駐し行があれば矩形を持つ()
        {
            Model.AddNotification(Earned(2, 1, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(300);
            Assert.Greater(Host.worldBound.width, 0f);
            Assert.Greater(Host.worldBound.height, 0f);
            AdvancePanel(NotificationModel.DisplayMilliseconds);
            AdvancePanel(0);
            Assert.IsNotNull(Host.panel);
        }

        [Test]
        public void 新着は最上段に積まれる()
        {
            Model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            Model.AddNotification(Earned(9, 1, "itemEarned.mined"));
            AdvancePanel(0);
            Assert.That(RowAt(0).Row.Content, Is.TypeOf<EarnedNotificationContent>().With.Property(nameof(EarnedNotificationContent.ItemId)).EqualTo(new ItemId(9)));
            Assert.Less(RowAt(0).worldBound.y, RowAt(1).worldBound.y);
        }

        [Test]
        public void 倍率が変わると左端は実画面の16pxに当たる値へ置き直され縦の中央線から下へ積む()
        {
            Model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            AdvancePanel(0);
            foreach (var stageScale in new[] { 1f, 1.5f, 1f })
            {
                // 16:9 どうしの解像度変更（パネルの寸法は変わらず倍率だけ変わる）を StageScaleSource の値の変化で起こす
                // Drive a 16:9-to-16:9 resolution change (panel size unchanged, only the scale changes) through the StageScaleSource value
                StageScaleForTest.Value = stageScale;
                AdvancePanel(0);
                // 当てた値は 16÷倍率ちょうど。L1 のパネルは倍率 1 で配置を整数 px に丸めるので、解決後の値は丸めの幅で見る
                // The written value is exactly 16 / scale; the L1 panel lays out at scale 1 and rounds to whole px, so the resolved value allows that rounding
                Assert.AreEqual(16f / stageScale, Host.style.left.value.value, 0.0001f, $"written left at scale {stageScale}");
                Assert.AreEqual(16f / stageScale, Host.resolvedStyle.left, 0.5f, $"left at scale {stageScale}");
                Assert.AreEqual(Host.parent.worldBound.height * 0.5f, Host.worldBound.y - Host.parent.worldBound.y, 0.5f, $"center line at scale {stageScale}");
            }
        }

        [Test]
        public void 行の角と中の要素は入力を奪わない()
        {
            Model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(300);
            var row = RowAt(0);
            var corners = new[] { new UnityEngine.Vector2(row.worldBound.xMin + 1f, row.worldBound.yMin + 1f), new UnityEngine.Vector2(row.worldBound.xMax - 1f, row.worldBound.yMax - 1f), row.Q(className: "notification-row__icon").worldBound.center };
            foreach (var point in corners)
            {
                var picked = Host.panel.Pick(point);
                Assert.IsFalse(picked != null && (picked == Host || Host.Contains(picked)), $"picked {picked?.name} at {point}");
            }
            row.Query<VisualElement>().ForEach(element => Assert.AreEqual(PickingMode.Ignore, element.pickingMode, element.name));
        }

        [Test]
        public void 言語を変えると表示中の行を解き直す()
        {
            Model.AddNotification(Achievement("achievement.researchCompleted", Wire("achievement.researchCompleted", new Dictionary<string, string> { ["research"] = Guid.Empty.ToString() }), NoIcon));
            AdvancePanel(0);
            StringAssert.Contains("Research completed", RowAt(0).Q<Label>(className: "notification-row__text").text);
            var callsBefore = TextLookup.ContentTextCalls;

            Localize.TrySetChosenLanguage("japanese");
            AdvancePanel(0);
            StringAssert.Contains("研究完了", RowAt(0).Q<Label>(className: "notification-row__text").text);
            Assert.Greater(TextLookup.ContentTextCalls, callsBefore);
        }

        [Test]
        public void ホストと行は入力を奪わない()
        {
            Model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(300);
            var picked = Host.panel.Pick(RowAt(0).worldBound.center);
            Assert.IsFalse(picked != null && (picked == Host || Host.Contains(picked)), $"picked {picked?.name}");
        }

        [Test]
        public void スタイル解決前の描画は解決後に出る()
        {
            var model = new NotificationModel();
            var early = new NotificationHostView(model, new FakeNotificationTextLookup(), new FakeNotificationRowIcons());
            model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            early.Render(model.Current.Value);
            Assert.AreEqual(0, early.childCount);
            Mount(early);
            AdvancePanel(0);
            Assert.AreEqual(1, early.childCount);
        }
    }
}
