using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Slots.Item
{
    /// <summary>
    /// ItemSlot の所持/必要の表示を確かめる（L1）
    /// Verifies ItemSlot owned/required display (L1)
    /// </summary>
    public class ItemSlotShortageTest : UiTestHost
    {
        private const string KnobPath = "Assets/Scripts/Client.Tests/Ui/Components/Slots/SlotKnobOverrides.uss";

        private bool _hadSavedLanguageCode;
        private string _savedLanguageCode;

        [SetUp]
        public void PinEnglish()
        {
            // 言語の保存値を退避し英語に固定
            // Save the stored language and pin English
            _hadSavedLanguageCode = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            _savedLanguageCode = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            Localize.Initialize();
            Localize.TrySetChosenLanguage("english");
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

        /// <web-test file="src/shared/ui/ItemSlot/index.test.ts">shortageは所持/必要の順で数値を出し、ツールチップにも同じ対応で載せる</web-test>
        [Test]
        public void 所持と必要の順で出しツールチップは不足の文にする()
        {
            var slot = Mount(null);
            var materialText = UiText.FromVerbatim("Iron 3/5");
            slot.SetContent(new ItemSlotContent(SlotTestIcons.WithTexture("1", "Iron"), 3, ItemSlotMarks.None, new ItemSlotShortage(3, 5, true, materialText), null, null));
            simulate.FrameUpdate();

            Assert.AreEqual("3/5", slot.Q<Label>(null, "item-slot__shortage", "outlined-count-label--glyph").text);
            Assert.IsTrue(HoverTooltipMark.TryFindMarked(slot.Q<SlotFrame>().contentContainer, out _, out var text));
            Assert.AreSame(materialText, text);
        }

        /// <web-test file="src/shared/ui/ItemSlot/index.test.ts">充足している素材のshortageは赤くしない</web-test>
        /// <web-test file="src/shared/ui/ItemSlot/index.test.ts">必要数を満たした実物入りスロットは赤くしない</web-test>
        [Test]
        public void 足りていれば黒で足りなければ赤にする()
        {
            var enough = Mount(null);
            enough.SetContent(new ItemSlotContent(SlotTestIcons.WithTexture("1", "Iron"), 5, ItemSlotMarks.None, new ItemSlotShortage(5, 5, false, null), null, null));
            var lacking = Mount(null);
            lacking.SetContent(new ItemSlotContent(SlotTestIcons.WithTexture("1", "Iron"), 1, ItemSlotMarks.None, new ItemSlotShortage(1, 5, true, null), null, null));
            simulate.FrameUpdate();

            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--count-text"), enough.Q(null, "item-slot__shortage", "outlined-count-label--glyph").resolvedStyle.color, "enough");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--text-insufficient"), lacking.Q(null, "item-slot__shortage", "outlined-count-label--glyph").resolvedStyle.color, "lacking");
        }

        /// <web-test file="src/shared/ui/ItemSlot/index.test.ts">実物入りでshortageがあると個数バッジを出さず所持/必要の1か所にまとめる</web-test>
        [Test]
        public void 実物入りで所持と必要があれば個数バッジを出さない()
        {
            var slot = Mount(null);
            slot.SetContent(new ItemSlotContent(SlotTestIcons.WithTexture("1", "Iron"), 3, ItemSlotMarks.None, new ItemSlotShortage(3, 5, true, null), null, null));
            simulate.FrameUpdate();

            Assert.AreEqual(DisplayStyle.None, slot.Q(null, "slot-content__count", "outlined-count-label--glyph").resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.Flex, slot.Q(null, "item-slot__shortage", "outlined-count-label--glyph").resolvedStyle.display);
        }

        /// <web-test file="src/shared/ui/ItemSlot/index.test.ts">shortageの有無に関わらず同じラッパー層で包み、数字だけ出し分ける</web-test>
        [Test]
        public void 所持と必要の有無で同じ枠と中身を使い回し数字だけ出し分ける()
        {
            var slot = Mount(null);
            var frameBefore = slot.Q<SlotFrame>();
            slot.SetContent(new ItemSlotContent(SlotTestIcons.WithTexture("1", "Iron"), 3, ItemSlotMarks.None, new ItemSlotShortage(3, 5, true, null), null, null));
            slot.SetContent(new ItemSlotContent(SlotTestIcons.WithTexture("1", "Iron"), 3, ItemSlotMarks.None, null, null, null));
            simulate.FrameUpdate();

            Assert.AreSame(frameBefore, slot.Q<SlotFrame>());
            Assert.AreEqual(DisplayStyle.None, slot.Q(null, "item-slot__shortage", "outlined-count-label--glyph").resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.Flex, slot.Q(null, "slot-content__count", "outlined-count-label--glyph").resolvedStyle.display);
        }

        /// <web-test file="src/shared/ui/ItemSlot/index.test.ts">tooltipKeyがnullなら素材の語彙でなくアイテム名のツールチップを保つ</web-test>
        [Test]
        public void 不足の文が無ければアイテム名のツールチップを保つ()
        {
            var slot = Mount(null);
            var icon = SlotTestIcons.WithTexture("1", "Iron");
            slot.SetContent(new ItemSlotContent(icon, 3, ItemSlotMarks.None, new ItemSlotShortage(3, 5, true, null), null, null));

            Assert.IsTrue(HoverTooltipMark.TryFindMarked(slot.Q<SlotFrame>().contentContainer, out _, out var text));
            Assert.AreSame(icon.Label, text);
        }

        [Test]
        public void 所持と必要は枠の外へつまみの位置と大きさで出る()
        {
            var slot = Mount("slot-knob-shortage");
            slot.SetContent(new ItemSlotContent(SlotTestIcons.WithTexture("1", "Iron"), 3, ItemSlotMarks.None, new ItemSlotShortage(3, 5, true, null), null, null));
            simulate.FrameUpdate();

            // 画素丸めのため 0.5px まで許す
            // Allow 0.5px because sizes snap to pixels
            var label = slot.Q<Label>(null, "item-slot__shortage", "outlined-count-label--glyph");
            var frame = slot.Q<SlotFrame>();
            Assert.AreEqual(13f, label.resolvedStyle.fontSize, 0.01f);
            Assert.AreEqual(frame.layout.width - 3f, label.layout.xMax, 0.5f);
            Assert.AreEqual(frame.layout.height - 2f, label.layout.yMax, 0.5f);
            Assert.AreEqual(PickingMode.Ignore, label.pickingMode);
        }

        [Test]
        public void 所持と必要の白い縁は字の後ろの縁だけの層が同じ位置で描く()
        {
            var slot = Mount(null);
            slot.SetContent(new ItemSlotContent(SlotTestIcons.WithTexture("1", "Iron"), 1, ItemSlotMarks.None, new ItemSlotShortage(1, 5, true, null), null, null));
            simulate.FrameUpdate();

            // 縁の層は字より前の子で、字と同じ文字・位置を持ち、赤字の状態は字の層だけに付く
            // The stroke layer is an earlier child with the same text and place; the lacking red applies only to the glyph layer
            var stroke = slot.Q<Label>(null, "item-slot__shortage", "outlined-count-label--stroke");
            var label = slot.Q<Label>(null, "item-slot__shortage", "outlined-count-label--glyph");
            Assert.Less(slot.IndexOf(stroke), slot.IndexOf(label));
            Assert.AreEqual("1/5", stroke.text);
            Assert.AreEqual(label.layout, stroke.layout);
            Assert.AreEqual(0f, label.resolvedStyle.unityTextOutlineWidth, 0.01f);
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--icon-text-stroke-width"), stroke.resolvedStyle.unityTextOutlineWidth, 0.01f);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--icon-text-stroke-light"), stroke.resolvedStyle.color, "stroke fill");
            Assert.AreEqual(PickingMode.Ignore, stroke.pickingMode);
        }

        [Test]
        public void 所持と必要が無くなれば不足の状態を外す()
        {
            var slot = Mount(null);
            var icon = SlotTestIcons.WithTexture("1", "Iron");
            slot.SetContent(new ItemSlotContent(icon, 3, ItemSlotMarks.None, new ItemSlotShortage(3, 5, true, null), null, null));
            simulate.FrameUpdate();
            Assert.IsTrue(slot.ClassListContains("item-slot--lack"));
            slot.SetContent(new ItemSlotContent(icon, 3, ItemSlotMarks.None, null, null, null));
            simulate.FrameUpdate();
            Assert.IsFalse(slot.ClassListContains("item-slot--lack"));
        }

        private ItemSlot Mount(string knobClass)
        {
            var screen = new VisualElement();
            if (knobClass != null)
            {
                var knobs = AssetDatabase.LoadAssetAtPath<StyleSheet>(KnobPath);
                Assert.IsNotNull(knobs, KnobPath);
                screen.styleSheets.Add(knobs);
                screen.AddToClassList(knobClass);
            }
            // Web の格子のマスと同じく、包みの幅を枠の幅にそろえる（素の column の親は子を横に伸ばす）
            // Like a Web grid cell, keep the wrapper as wide as the frame (a plain column parent stretches children)
            screen.style.alignItems = Align.FlexStart;
            var slot = new ItemSlot("slot");
            screen.Add(slot);
            rootVisualElement.Add(screen);
            simulate.FrameUpdate();
            return slot;
        }
    }
}
