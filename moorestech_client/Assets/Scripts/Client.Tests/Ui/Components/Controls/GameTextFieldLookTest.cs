using Client.Localization;
using Client.Tests.Ui.Screens;
using Client.Ui.Components.Controls;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Controls
{
    /// <summary>
    /// GameTextField の見た目 2 種（Web の textAreaField／Mantine TextInput）・フォーカスの輪・置き文字の言語追従・無効時の見た目を確かめる（L1）
    /// Verifies GameTextField's two looks (Web textAreaField / Mantine TextInput), focus ring, placeholder language tracking and disabled look (L1)
    /// </summary>
    public class GameTextFieldLookTest : ScreenTestHost
    {
        private const string TestId = "game-text-field-look-test";
        private const string InputClassName = "unity-base-text-field__input";

        // --bevel-1 の線を --bevel-1 離して外へ出すので、輪は入力欄より 2 倍ぶん外にある
        // The ring is a --bevel-1 line offset by --bevel-1, so it sits twice that outside the field
        private const float FocusRingOutset = 1.56575f;

        [Test]
        public void GaugeInsetはWebの記述欄と同じ面と枠と余白で角が無い()
        {
            var field = Add(GameTextFieldLook.GaugeInset, true);
            var input = field.Field.Q(className: InputClassName);

            // --gauge-track = --surface-navy（α 補正後 rgba(10, 14, 27, 0.971)）・--bevel-c1 rgb(56 58 72)・--text-default #eee
            // --gauge-track = --surface-navy (alpha-corrected rgba(10, 14, 27, 0.971)), --bevel-c1 rgb(56 58 72), --text-default #eee
            ControlStyleAssert.AssertColor(new Color(10f / 255f, 14f / 255f, 27f / 255f, 0.971f), input.resolvedStyle.backgroundColor);
            ControlStyleAssert.AssertColor(new Color(56f / 255f, 58f / 255f, 72f / 255f, 1f), input.resolvedStyle.borderTopColor);
            ControlStyleAssert.AssertColor("#EEEEEE", input.resolvedStyle.color);
            Assert.AreEqual(0.782875f, input.resolvedStyle.borderTopWidth, 0.25f);
            Assert.AreEqual(0f, input.resolvedStyle.borderTopLeftRadius);

            // 内側の余白は ModeSwitch と同じ。UITK は余白を画素へ丸めるので 9.6px は 10 に解ける
            // Inner padding matches ModeSwitch; UITK snaps padding to pixels, so 9.6px resolves to 10
            Assert.AreEqual(5f, input.resolvedStyle.paddingTop, 0.01f);
            Assert.AreEqual(9.6f, input.resolvedStyle.paddingLeft, 0.5f);
            Assert.AreEqual(WhiteSpace.Normal, input.resolvedStyle.whiteSpace);
        }

        [Test]
        public void GaugeInsetはフォーカス中だけ外側に高コントラストの輪を出す()
        {
            var field = Add(GameTextFieldLook.GaugeInset, false);
            var ring = field.Q(className: GameTextField.FocusRingUssClassName);
            Assert.AreEqual(DisplayStyle.None, ring.resolvedStyle.display);
            Assert.AreEqual(PickingMode.Ignore, ring.pickingMode);

            field.Field.Focus();
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.Flex, ring.resolvedStyle.display);
            ControlStyleAssert.AssertColor("#FFFFFF", ring.resolvedStyle.borderTopColor);
            Assert.AreEqual(field.Field.worldBound.xMin - FocusRingOutset, ring.worldBound.xMin, 0.5f);
            Assert.AreEqual(field.Field.worldBound.yMax + FocusRingOutset, ring.worldBound.yMax, 0.5f);

            // 枠の色はフォーカスで変えない（Web は outline だけを足す）
            // Focus does not recolour the border (the Web only adds an outline)
            ControlStyleAssert.AssertColor(new Color(56f / 255f, 58f / 255f, 72f / 255f, 1f), field.Field.Q(className: InputClassName).resolvedStyle.borderTopColor);

            field.Field.Blur();
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.None, ring.resolvedStyle.display);
        }

        [Test]
        public void MantineInputはMantineのTextInputのsmでフォーカスで枠が青になり輪は出さない()
        {
            // --input-height-sm 36px・--input-padding 12px・枠 1px dark-4 #424242・面 dark-6 #2e2e2e・文字 dark-0 #c9c9c9・14px・角 4px・フォーカス blue-8 #1971c2
            // --input-height-sm 36px, --input-padding 12px, 1px dark-4 #424242 border, dark-6 #2e2e2e face, dark-0 #c9c9c9 text, 14px, 4px radius, blue-8 #1971c2 on focus
            var field = Add(GameTextFieldLook.MantineInput, false);
            var input = field.Field.Q(className: InputClassName);
            Assert.AreEqual(36f, input.resolvedStyle.height, 0.5f);
            Assert.AreEqual(12f, input.resolvedStyle.paddingLeft, 0.01f);
            Assert.AreEqual(1f, input.resolvedStyle.borderTopWidth, 0.01f);
            Assert.AreEqual(4f, input.resolvedStyle.borderTopLeftRadius, 0.01f);
            Assert.AreEqual(14f, input.resolvedStyle.fontSize, 0.01f);
            ControlStyleAssert.AssertColor("#424242", input.resolvedStyle.borderTopColor);
            ControlStyleAssert.AssertColor("#2E2E2E", input.resolvedStyle.backgroundColor);
            ControlStyleAssert.AssertColor("#C9C9C9", input.resolvedStyle.color);

            field.Field.Focus();
            simulate.FrameUpdate();
            ControlStyleAssert.AssertColor("#1971C2", input.resolvedStyle.borderTopColor);
            Assert.AreEqual(DisplayStyle.None, field.Q(className: GameTextField.FocusRingUssClassName).resolvedStyle.display);
        }

        [Test]
        public void 置き文字は今の言語で解決し言語を変えると解決し直す()
        {
            var field = Add(GameTextFieldLook.GaugeInset, true);
            field.SetPlaceholder(new UiText(LocalizationKeys.Ui.BugReport.Placeholder));
            var english = field.Field.textEdition.placeholder;
            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.BugReport.Placeholder), english);

            // 言語の保存値は ScreenTestHost の TearDown が戻す
            // ScreenTestHost's TearDown restores the saved language
            Localize.TrySetChosenLanguage("japanese");
            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.BugReport.Placeholder), field.Field.textEdition.placeholder);
            Assert.AreNotEqual(english, field.Field.textEdition.placeholder);
        }

        [Test]
        public void GaugeInsetは無効でもWebと同じく見た目を変えない()
        {
            // Web の textarea は作者の色が UA の無効色に勝つので、無効でも面と文字が変わらない。既定テーマの半透明を付けない
            // In the Web the author colours beat the UA disabled colours, so face and text stay; no default-theme translucency
            var field = Add(GameTextFieldLook.GaugeInset, true);
            field.Field.SetEnabled(false);
            simulate.FrameUpdate();

            Assert.AreEqual(1f, field.Field.resolvedStyle.opacity, 0.001f);
            Assert.AreEqual(1f, field.Field.Q(className: InputClassName).resolvedStyle.opacity, 0.001f);
            ControlStyleAssert.AssertColor("#EEEEEE", field.Field.Q(className: InputClassName).resolvedStyle.color);
        }

        private GameTextField Add(GameTextFieldLook look, bool multiline)
        {
            var field = new GameTextField(TestId, multiline, look);
            field.style.width = 356;
            if (multiline) field.style.height = 72;
            return Mount(field);
        }
    }
}
