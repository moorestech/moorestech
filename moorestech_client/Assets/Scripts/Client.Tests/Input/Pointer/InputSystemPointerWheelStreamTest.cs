using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Input.Pointer;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Client.Tests.Input.Pointer
{
    /// <summary>
    /// ホイールが回った時だけ縦のノッチを流し、マウスが無ければ警告 1 回
    /// Publishes vertical notches only when the wheel turns; warns once without a mouse
    /// </summary>
    public class InputSystemPointerWheelStreamTest : InputTestFixture
    {
        [Test]
        public void 下へ1ノッチ回すとマイナス1を1回流す()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            var received = Subscribe(new InputSystemPointerWheelStream());

            Set(mouse.scroll, new Vector2(0f, -1f));

            Assert.AreEqual(new[] { -1f }, received.ToArray());
        }

        [Test]
        public void ホイールの無い位置だけの変化は流さない()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            var received = Subscribe(new InputSystemPointerWheelStream());

            Set(mouse.position, new Vector2(50f, 60f));

            Assert.IsEmpty(received);
        }

        [Test]
        public void Editorの更新で届いたホイールは流さない()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            var received = Subscribe(new InputSystemPointerWheelStream());
            var rawUpdates = new List<InputUpdateType>();
            InputSystem.onEvent += (_, device) => { if (device == mouse) rawUpdates.Add(InputState.currentUpdateType); };

            // Game View にフォーカスが無いと、ポインタのイベントは Editor の更新で処理される（InputManager.cs:3320-3345）
            // Without Game View focus, pointer events are processed in the Editor update (InputManager.cs:3320-3345)
            InputTestDriver.LoseGameFocus(this);
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0f, -1f) });
            InputTestDriver.RunUpdate(InputUpdateType.Editor);

            // 前提: 生の onEvent は Editor の更新で受けた（受けていなければこの件は何も試していない）
            // Premise: the raw onEvent saw it in an Editor update (otherwise this case tests nothing)
            CollectionAssert.Contains(rawUpdates, InputUpdateType.Editor);
            Assert.IsEmpty(received);
        }

        [Test]
        public void マウスが無いと警告を出して何も流さない()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"Mouse\.current is null"));
            var received = Subscribe(new InputSystemPointerWheelStream());

            var keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.aKey);

            Assert.IsEmpty(received);
        }

        private static List<float> Subscribe(InputSystemPointerWheelStream stream)
        {
            var received = new List<float>();
            stream.OnWheelNotchesScrolled.Subscribe(received.Add);
            return received;
        }
    }
}
