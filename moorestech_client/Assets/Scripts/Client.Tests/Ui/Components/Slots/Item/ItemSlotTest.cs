using System.Collections.Generic;
using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Icons;
using Client.Ui.Foundation.Tooltip;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Slots.Item
{
    /// <summary>
    /// ItemSlot の減光・個数・身振りを確かめる（L1）
    /// Verifies ItemSlot dimming, count and gestures (L1)
    /// </summary>
    public class ItemSlotTest : UiTestHost
    {
        private ItemSlot _slot;
        private SlotFrame _frame;
        private bool _hadSavedLanguageCode;
        private string _savedLanguageCode;

        [SetUp]
        public void AddSlot()
        {
            // 言語の保存値を退避し英語に固定
            // Save the stored language and pin English
            _hadSavedLanguageCode = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            _savedLanguageCode = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            Localize.Initialize();
            Localize.TrySetChosenLanguage("english");
            _slot = new ItemSlot("inventory-slot-0");
            rootVisualElement.Add(_slot);
            _frame = _slot.Q<SlotFrame>("inventory-slot-0");
            simulate.FrameUpdate();
        }

        [TearDown]
        public void NoUnexpectedLogs()
        {
            // 言語の復元を先に済ませ、検証が失敗しても英語固定を残さない
            // Restore the language first so a failed assertion never leaves English pinned
            rootVisualElement.Clear();
            if (_hadSavedLanguageCode) PlayerPrefs.SetString(Localize.LanguagePreferenceKey, _savedLanguageCode);
            else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            Localize.Initialize();
            LogAssert.NoUnexpectedReceived();
        }

        /// <web-test file="src/shared/ui/ItemSlot/index.test.ts">item master のGuidから辞書名をtooltipと代替テキストへ使う</web-test>
        [Test]
        public void 表示中のアイコンの名前を枠のツールチップに使う()
        {
            var icon = SlotTestIcons.WithTexture("1", "Iron Plate");
            _slot.SetContent(new ItemSlotContent(icon, 3, new ItemSlotMarks(false, false, false), null, null, null));

            Assert.IsTrue(HoverTooltipMark.TryFindMarked(_frame.contentContainer, out var marked, out var text));
            Assert.AreSame(_frame, marked);
            Assert.AreSame(icon.Label, text);
        }

        /// <web-test file="src/shared/ui/ItemSlot/index.test.ts">不足状態をスロット枠のdata属性へ伝える</web-test>
        /// <web-test file="src/shared/ui/ItemSlot/index.test.ts">不足属性へ従来と同じ40%減光を設定する</web-test>
        /// <web-test file="e2e/tests/regression/itemSlot.spec.ts">素材不足スロットはdata-insufficientと40% opacityを持つ</web-test>
        [Test]
        public void 不足は枠に状態のクラスを付け枠ごと減光する()
        {
            _slot.SetContent(new ItemSlotContent(SlotTestIcons.WithTexture("1", "Iron"), 1, new ItemSlotMarks(false, false, true), null, null, null));
            simulate.FrameUpdate();

            // Web の 40% そのままのトークンの値になる
            // Resolves to the token holding the Web 40% as is
            Assert.IsTrue(_frame.ClassListContains("slot-frame--insufficient"));
            Assert.AreEqual(float.Parse(ThemeTokenFile.Resolve("--slot-frame-insufficient-opacity")), GroupOpacity.Read(_frame), 0.001f);
        }

        /// <web-test file="src/shared/ui/ItemSlot/index.test.ts">不足状態の省略時はdata属性を付けない</web-test>
        [Test]
        public void 不足でなければ状態のクラスを付けない()
        {
            _slot.SetContent(new ItemSlotContent(SlotTestIcons.WithTexture("1", "Iron"), 1, new ItemSlotMarks(false, false, false), null, null, null));

            Assert.IsFalse(_frame.ClassListContains("slot-frame--insufficient"));
        }

        /// <web-test file="src/shared/ui/ItemSlot/index.test.ts">4桁の個数は千区切りで描く</web-test>
        [Test]
        public void 四桁の個数は千区切りで描く()
        {
            _slot.SetContent(new ItemSlotContent(SlotTestIcons.WithTexture("1", "Iron"), 1500, new ItemSlotMarks(false, false, false), null, null, null));
            simulate.FrameUpdate();

            var count = _slot.Q<Label>(null, "slot-content__count", "outlined-count-label--glyph");
            Assert.AreEqual("1,500", count.text);
            Assert.AreEqual(DisplayStyle.Flex, count.resolvedStyle.display);
        }

        [Test]
        public void 空きマスにしたら絵とツールチップを外す()
        {
            _slot.SetContent(new ItemSlotContent(SlotTestIcons.WithTexture("1", "Iron"), 2, new ItemSlotMarks(false, false, false), null, null, null));
            _slot.SetContent(new ItemSlotContent(null, null, new ItemSlotMarks(false, false, false), null, null, null));
            simulate.FrameUpdate();

            Assert.IsFalse(HoverTooltipMark.TryFindMarked(_frame.contentContainer, out _, out _));
            Assert.IsFalse(_frame.ClassListContains("slot-frame--filled"));
            Assert.AreEqual(DisplayStyle.None, _slot.Q(className: "game-icon__image").resolvedStyle.display);
        }

        [Test]
        public void 名前の無い絵に替えたら文の無い印を残さずツールチップを外す()
        {
            _slot.SetContent(new ItemSlotContent(SlotTestIcons.WithTexture("1", "Iron"), 2, new ItemSlotMarks(false, false, false), null, null, null));
            _slot.SetContent(new ItemSlotContent(new UiIcon(SlotTestIcons.SharedTexture, "1", null), 2, new ItemSlotMarks(false, false, false), null, null, null));
            simulate.FrameUpdate();

            Assert.IsFalse(HoverTooltipMark.TryFindMarked(_frame.contentContainer, out _, out _));
            Assert.IsTrue(_frame.ClassListContains("slot-frame--filled"));
        }

        [Test]
        public void ゴーストは枠に状態を付けて中身だけを透かす()
        {
            var ghost = new ItemSlotGhost(SlotTestIcons.WithTexture("2", "Copper"), 3);
            _slot.SetContent(new ItemSlotContent(null, null, new ItemSlotMarks(false, false, false), null, ghost, null));
            simulate.FrameUpdate();

            Assert.IsTrue(_frame.ClassListContains("slot-frame--ghost"));
            Assert.AreEqual("3", _slot.Q<Label>(null, "slot-content__count", "outlined-count-label--glyph").text);
        }

        [Test]
        public void 枠の身振りをそのまま流す()
        {
            var gestures = new List<SlotGesture>();
            _slot.OnSlotGestured.Subscribe(gestures.Add);
            simulate.Click(_frame);

            Assert.IsTrue(gestures.Exists(g => g.Kind == SlotGestureKind.LeftDown));
        }
    }
}
