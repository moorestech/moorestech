using Client.Localization;
using Client.Tests.Ui.Screens;
using Client.Ui.Components.Controls;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using static Client.Tests.Ui.Components.ComponentStyleAssert;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Controls
{
    /// <summary>
    /// GameTextField の見た目2種・フォーカスの輪・置き文字・無効時を確かめる
    /// Verifies GameTextField's two looks, focus ring, placeholder and disabled look
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

            // --gauge-track = --surface-navy（Web の rgba(10, 14, 27, 0.8)）・--bevel-c1 rgb(56 58 72)・--text-default #eee
            // --gauge-track = --surface-navy (the Web rgba(10, 14, 27, 0.8)), --bevel-c1 rgb(56 58 72), --text-default #eee
            AssertColor(Rgba(10, 14, 27, 0.8f), input.resolvedStyle.backgroundColor, "face");
            AssertColor(Rgba(56, 58, 72, 1f), input.resolvedStyle.borderTopColor, "border");
            AssertColor(Hex("#EEEEEE"), input.resolvedStyle.color, "text");
            Assert.AreEqual(0.782875f, input.resolvedStyle.borderTopWidth, 0.25f);
            Assert.AreEqual(0f, input.resolvedStyle.borderTopLeftRadius);

            // 内側の余白は ModeSwitch と同じ。UITK は余白を画素へ丸めるので 9.6px は 10 に解ける
            // Inner padding matches ModeSwitch; UITK snaps padding to pixels, so 9.6px resolves to 10
            Assert.AreEqual(5f, input.resolvedStyle.paddingTop, 0.01f);
            Assert.AreEqual(9.6f, input.resolvedStyle.paddingLeft, 0.5f);
            Assert.AreEqual(WhiteSpace.Normal, input.resolvedStyle.whiteSpace);

            // 字は Mantine の body の 16px を継ぐ（Web の記述欄と検索欄は font-size を書かない）
            // Text inherits the Mantine body 16px (the Web textarea and search input set no font-size)
            Assert.AreEqual(16f, input.resolvedStyle.fontSize, 0.01f);

            // 字は余白の内側からすぐ始まる
            // Text starts right inside the padding
            var text = input.Q(className: "unity-text-element--inner-input-field-component");
            Assert.AreEqual(0f, text.resolvedStyle.paddingLeft, 0.01f);
            Assert.AreEqual(0f, text.resolvedStyle.paddingRight, 0.01f);
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
            AssertColor(Hex("#FFFFFF"), ring.resolvedStyle.borderTopColor, "border");
            Assert.AreEqual(field.Field.worldBound.xMin - FocusRingOutset, ring.worldBound.xMin, 0.5f);
            Assert.AreEqual(field.Field.worldBound.yMax + FocusRingOutset, ring.worldBound.yMax, 0.5f);

            // 枠の色はフォーカスで変えない
            // Focus does not recolour the border
            AssertColor(Rgba(56, 58, 72, 1f), field.Field.Q(className: InputClassName).resolvedStyle.borderTopColor, "border");

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
            AssertColor(Hex("#424242"), input.resolvedStyle.borderTopColor, "border");
            AssertColor(Hex("#2E2E2E"), input.resolvedStyle.backgroundColor, "face");
            AssertColor(Hex("#C9C9C9"), input.resolvedStyle.color, "text");

            // キャレットは文字の色（Chrome の caret-color: auto）。UITK の既定の黒にしない
            // The caret takes the text colour (Chrome caret-color: auto), not UITK's default black
            AssertColor(Hex("#C9C9C9"), field.Field.textSelection.cursorColor, "caret");

            field.Field.Focus();
            simulate.FrameUpdate();
            AssertColor(Hex("#1971C2"), input.resolvedStyle.borderTopColor, "border");
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
            AssertColor(Hex("#EEEEEE"), field.Field.Q(className: InputClassName).resolvedStyle.color, "text");
        }

        private GameTextField Add(GameTextFieldLook look, bool multiline)
        {
            var field = new GameTextField(TestId, multiline, look);
            field.style.width = 354;
            if (multiline) field.style.height = 72;
            return Mount(field);
        }
    }
}
