using System.Text.RegularExpressions;
using Client.Input.Pointer;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Client.Tests.Input.Pointer
{
    /// <summary>
    /// 左ボタンが押下から解放へ変わった時だけ 1 回流し、マウスが無ければ警告 1 回
    /// Publishes once only when the left button goes from pressed to released; warns once without a mouse
    /// </summary>
    public class InputSystemPointerButtonStreamTest : InputTestFixture
    {
        [Test]
        public void 左ボタンを離すと1回流す()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            var released = Count(new InputSystemPointerButtonStream());

            Press(mouse.leftButton);
            Assert.AreEqual(0, released[0], "押下では流さない");
            Release(mouse.leftButton);

            Assert.AreEqual(1, released[0]);
        }

        [Test]
        public void 押下の状態をマウスの実状態で答える()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            var stream = new InputSystemPointerButtonStream();

            Assert.IsFalse(stream.IsPrimaryButtonPressed(), "初期");
            Press(mouse.leftButton);
            Assert.IsTrue(stream.IsPrimaryButtonPressed(), "押下後");
            Release(mouse.leftButton);
            Assert.IsFalse(stream.IsPrimaryButtonPressed(), "解放後");
        }

        [Test]
        public void 解放を流さずにデバイスがリセットされると押下はfalseに戻る()
        {
            // フォーカス喪失のデバイスリセット等で解放を取りこぼしても、押下は実状態を答える
            // Even if a release is missed through a device reset on focus loss, the pressed state reports the actual state
            var mouse = InputSystem.AddDevice<Mouse>();
            var stream = new InputSystemPointerButtonStream();
            Press(mouse.leftButton);
            Assert.IsTrue(stream.IsPrimaryButtonPressed(), "押下後");

            InputSystem.ResetDevice(mouse);

            Assert.IsFalse(stream.IsPrimaryButtonPressed());
        }

        [Test]
        public void マウスがリセットされるとリセットを1回流し押下を忘れる()
        {
            // リセット後の押下なしの状態イベントで解放を流さないことも見る（押下の記憶が下りている）
            // Also checks a later unpressed state event emits no release (the remembered press was dropped)
            var mouse = InputSystem.AddDevice<Mouse>();
            var stream = new InputSystemPointerButtonStream();
            var reset = new int[1];
            stream.OnPrimaryButtonReset.Subscribe(_ => reset[0]++);
            Press(mouse.leftButton);
            var released = Count(stream);

            InputSystem.ResetDevice(mouse);
            Assert.AreEqual(1, reset[0]);
            Assert.IsFalse(stream.IsPrimaryButtonPressed());
            Set(mouse.position, new Vector2(10f, 20f));

            Assert.AreEqual(0, released[0], "リセットの後に解放は流れない");
        }

        [Test]
        public void 位置だけの変化と右ボタンでは流さない()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            var released = Count(new InputSystemPointerButtonStream());

            Set(mouse.position, new Vector2(10f, 20f));
            Press(mouse.rightButton);
            Release(mouse.rightButton);

            Assert.AreEqual(0, released[0]);
        }

        [Test]
        public void 押したまま動かしても離すまで流さない()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            var released = Count(new InputSystemPointerButtonStream());

            Press(mouse.leftButton);
            Set(mouse.position, new Vector2(30f, 40f));
            Set(mouse.position, new Vector2(50f, 60f));
            Assert.AreEqual(0, released[0]);
            Release(mouse.leftButton);

            Assert.AreEqual(1, released[0]);
        }

        [Test]
        public void マウスが無いと警告を出して何も流さない()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"Mouse\.current is null"));
            var released = Count(new InputSystemPointerButtonStream());

            var keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.aKey);

            Assert.AreEqual(0, released[0]);
        }

        private static int[] Count(InputSystemPointerButtonStream stream)
        {
            var counter = new int[1];
            stream.OnPrimaryButtonReleased.Subscribe(_ => counter[0]++);
            return counter;
        }
    }
}
