using Client.Input;
using Client.Input.KeyPress;
using Client.Tests.Common;
using NUnit.Framework;
using UniRx;
using UnityEngine.InputSystem;

namespace Client.Tests.Ui.Crosscut.Input
{
    // 画面へキーを渡す口は抑止を InputKey の InputSuppressionFilter だけに任せる（A-246。Web inputLayer の入力欄の除外）
    // The key ports leave suppression to the InputKey's InputSuppressionFilter alone (A-246; the Web inputLayer text-field exclusion)
    public class UiKeyPressSourceSuppressionTest : InputTestFixture
    {
        public override void Setup()
        {
            base.Setup();
            TestReflection.ResetInputManagerCache();
        }

        public override void TearDown()
        {
            UiInputExclusivity.SetState(false, false);
            UiInputExclusivity.ClearEscapeLatchForTest();
            InputManager.UI.CloseUI.SetKeyDownForTest(false);
            TestReflection.ResetInputManagerCache();
            base.TearDown();
        }

        [Test]
        public void スキットの送りはEnterで流れ入力欄のフォーカス中は流れない()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var presses = 0;
            new SkitAdvanceKeyPressSource().OnPressed.Subscribe(_ => presses++);
            InputSystem.Update();
            Press(keyboard.enterKey);
            InputSystem.Update();
            Release(keyboard.enterKey);
            InputSystem.Update();
            UiInputExclusivity.SetState(false, true);
            Press(keyboard.enterKey);
            InputSystem.Update();
            Assert.AreEqual(1, presses);
        }

        // 旧の probe と同じく読んだ時点で抑止と ESC ラッチを評価する（D-8 の受け身の案）
        // As with the old probe, suppression and the ESC latch are evaluated at read time (D-8, the passive option)
        [Test]
        public void CloseUIはこのフレームの押下を読め入力欄のフォーカス中とESCを使ったフレームは偽()
        {
            // ESC ラッチは Keyboard.current の escapeKey が CloseUI の割り当てかで効く（InputKey.FilterPress）。無いと 3 つ目が素通りする
            // The ESC latch keys off whether Keyboard.current's escapeKey is bound to CloseUI (InputKey.FilterPress); without a keyboard the third assert passes through
            InputSystem.AddDevice<Keyboard>();
            var closeKey = new CloseUiKeyPressSource();
            InputManager.UI.CloseUI.SetKeyDownForTest(true);
            Assert.IsTrue(closeKey.WasPressedThisFrame());
            UiInputExclusivity.SetState(false, true);
            Assert.IsFalse(closeKey.WasPressedThisFrame());
            UiInputExclusivity.SetState(false, false);
            UiInputExclusivity.ConsumeEscapeThisFrame();
            Assert.IsFalse(closeKey.WasPressedThisFrame(), "CloseUI が ESC に割り当てられていればラッチで止まる");
        }

        [Test]
        public void 同じキーの口を2つ作っても抑止の判定はInputKeyの1か所()
        {
            var first = new SkitAdvanceKeyPressSource();
            var second = new SkitAdvanceKeyPressSource();
            InputManager.UI.SkitAdvance.SetKeyDownForTest(true);
            UiInputExclusivity.SetState(false, true);
            Assert.IsFalse(first.WasPressedThisFrame());
            Assert.IsFalse(second.WasPressedThisFrame());
            InputManager.UI.SkitAdvance.SetKeyDownForTest(false);
        }
    }
}
