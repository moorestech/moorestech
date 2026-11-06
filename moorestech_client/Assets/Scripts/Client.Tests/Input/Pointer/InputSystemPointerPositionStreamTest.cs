using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Input;
using Client.Input.Pointer;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Client.Tests.Input.Pointer
{
    /// <summary>
    /// ポインタ位置の購読口: マウスの位置が変わった時だけ HybridInput と同じ座標で流し、マウスが無ければ警告を 1 回出す
    /// Pointer position stream: publishes only on mouse position changes in HybridInput's coordinates, and warns once without a mouse
    /// </summary>
    public class InputSystemPointerPositionStreamTest : InputTestFixture
    {
        [Test]
        public void マウスの位置が変わるとHybridInputと同じ座標で流す()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            var received = Subscribe(new InputSystemPointerPositionStream());

            Set(mouse.position, new Vector2(120f, 80f));
            Set(mouse.position, new Vector2(300f, 40f));

            Assert.AreEqual(new[] { new Vector2(120f, 80f), new Vector2(300f, 40f) }, received.ToArray());
            Assert.AreEqual(received[^1], (Vector2)HybridInput.GetMousePosition());
        }

        [Test]
        public void 位置の変わらないボタンだけの変化は流さない()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            var received = Subscribe(new InputSystemPointerPositionStream());

            Set(mouse.position, new Vector2(50f, 60f));
            Press(mouse.leftButton);
            Release(mouse.leftButton);

            Assert.AreEqual(new[] { new Vector2(50f, 60f) }, received.ToArray());
        }

        [Test]
        public void マウスが無いと警告を出して何も流さない()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"Mouse\.current is null"));
            var received = Subscribe(new InputSystemPointerPositionStream());

            var keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.aKey);

            Assert.IsEmpty(received);
        }

        private static List<Vector2> Subscribe(IPointerPositionStream stream)
        {
            var received = new List<Vector2>();
            stream.OnPointerScreenPositionChanged.Subscribe(received.Add);
            return received;
        }
    }
}
