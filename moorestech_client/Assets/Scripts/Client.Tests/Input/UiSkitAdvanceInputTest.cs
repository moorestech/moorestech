using Client.Input;
using Client.Tests.Common;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Client.Tests.Input
{
    /// <summary>
    /// スキットの送りの Enter が入力アセットの束ねから InputKey へ届き、入力欄フォーカス中は抑止されることを確かめる（L0。前例 PlayableInteractInputTest）
    /// Verifies that the skit-advance Enter reaches the InputKey from the input asset bindings and is suppressed while a text field has focus (L0; precedent PlayableInteractInputTest)
    /// </summary>
    public class UiSkitAdvanceInputTest : InputTestFixture
    {
        public override void Setup()
        {
            base.Setup();

            // 他テストが張った InputManager の静的キャッシュは破棄済みデバイスを参照したままなので張り直す
            // Drop InputManager's static cache so it doesn't keep referencing a device disposed by an earlier test
            TestReflection.ResetInputManagerCache();
        }

        public override void TearDown()
        {
            UiInputExclusivity.SetState(false, false);
            TestReflection.ResetInputManagerCache();
            base.TearDown();
        }

        [Test]
        public void EnterとテンキーのEnterがSkitAdvanceとして読める()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var skitAdvance = InputManager.UI.SkitAdvance;
            // EditMode では WasPressedThisFrame が発火しないので購読で数える
            // WasPressedThisFrame never fires in EditMode, so count through the subscription
            var keyDowns = 0;
            skitAdvance.OnGetKeyDown += () => keyDowns++;
            InputSystem.Update();

            Press(keyboard.enterKey);
            InputSystem.Update();
            Release(keyboard.enterKey);
            InputSystem.Update();
            Press(keyboard.numpadEnterKey);
            InputSystem.Update();

            Assert.AreEqual(2, keyDowns);
        }

        [Test]
        public void 入力欄にフォーカスがある間はEnterをスキットへ渡さない()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var skitAdvance = InputManager.UI.SkitAdvance;
            var keyDowns = 0;
            skitAdvance.OnGetKeyDown += () => keyDowns++;
            InputSystem.Update();

            // 抑止の判定とログは InputSuppressionFilter が持ち、画面は自前で判定しない（D-29）
            // InputSuppressionFilter owns the suppression decision and its log; the screen does not judge it itself (D-29)
            UiInputExclusivity.SetState(false, true);
            Press(keyboard.enterKey);
            InputSystem.Update();
            Assert.AreEqual(0, keyDowns);
        }
    }
}
