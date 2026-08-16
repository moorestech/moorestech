using Client.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.Input
{
    /// <summary>
    ///     Altワープ直後の同フレーム読みが画面中央を返すことを固定する
    ///     Pins that a same-frame read right after the Alt warp returns the screen center
    /// </summary>
    public class InputManagerWarpCursorTest : InputTestFixture
    {
        private Mouse _mouse;

        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        [Test]
        public void WarpMouseCursorToScreenCenterAppliesInSameFrame()
        {
            // 中央以外へ寄せてからワープし、入力更新を挟まずに中央が読めることを確認する
            // Move the cursor off-center, warp, then read the center back without an input update in between
            var offCenter = ScreenCenter.GetPosition() + new Vector2(37f, 53f);
            Set(_mouse.position, offCenter);
            Assert.AreEqual(offCenter, (Vector2)HybridInput.GetMousePosition());

            InputManager.WarpMouseCursorToScreenCenter();
            Assert.AreEqual(ScreenCenter.GetPosition(), (Vector2)HybridInput.GetMousePosition());
        }
    }
}
