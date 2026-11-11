using System.Collections.Generic;
using Client.Input.ArrowKeys;
using NUnit.Framework;
using UniRx;
using UnityEngine.InputSystem;

namespace Client.Tests.Input.ArrowKeys
{
    /// <summary>
    /// ←/→ を押した瞬間だけ 1 回流し、押しっぱなし・他のキー・離しでは流さない（Web の window keydown の代わり）
    /// Publishes once only at the press of ← or →; holding, other keys and releases publish nothing (stands in for Web window keydown)
    /// </summary>
    public class InputSystemArrowKeyStreamTest : InputTestFixture
    {
        [Test]
        public void 左右の矢印の押下だけを1回ずつ流す()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var pressed = new List<HorizontalArrowKey>();
            new InputSystemArrowKeyStream().OnArrowPressed.Subscribe(pressed.Add);

            Press(keyboard.rightArrowKey);
            Set(keyboard.aKey, 1f);
            Release(keyboard.rightArrowKey);
            Press(keyboard.leftArrowKey);
            Press(keyboard.upArrowKey);

            CollectionAssert.AreEqual(new[] { HorizontalArrowKey.Right, HorizontalArrowKey.Left }, pressed);
        }

        [Test]
        public void リセットの後に押し直すとまた流す()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var pressed = new List<HorizontalArrowKey>();
            new InputSystemArrowKeyStream().OnArrowPressed.Subscribe(pressed.Add);

            Press(keyboard.rightArrowKey);
            InputSystem.ResetDevice(keyboard);
            Press(keyboard.rightArrowKey);

            Assert.AreEqual(2, pressed.Count);
        }
    }
}
