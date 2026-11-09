using Client.Tests.Ui.TestDoubles;
using Client.Ui.Foundation.Input.OsCursor;
using NUnit.Framework;

namespace Client.Tests.Ui.Input.OsCursor
{
    /// <summary>
    /// OS カーソルの要求スタックの調停（層の優先・同じ層の後入れ先出し・変化時だけ当てる・二重 Dispose）を固定する
    /// Pins the OS cursor request stack arbitration (layer priority, LIFO within a layer, apply only on change, double Dispose)
    /// </summary>
    public class UiOsCursorRequestStackTest
    {
        private FakeUiOsCursorApplier _applier;
        private UiOsCursorRequestStack _stack;

        [SetUp]
        public void CreateStack()
        {
            _applier = new FakeUiOsCursorApplier();
            _stack = new UiOsCursorRequestStack(_applier);
        }

        [Test]
        public void 同じ層は後から積んだ要求が勝ち_外すと前の要求へ戻り_全部外すと既定()
        {
            var first = _stack.Request(UiOsCursorShape.Pointer, UiOsCursorLayer.Surface);
            var second = _stack.Request(UiOsCursorShape.Grab, UiOsCursorLayer.Surface);
            second.Dispose();
            first.Dispose();
            CollectionAssert.AreEqual(new[] { UiOsCursorShape.Pointer, UiOsCursorShape.Grab, UiOsCursorShape.Pointer, UiOsCursorShape.Default }, _applier.AppliedShapes);
        }

        [Test]
        public void 上の層の要求は後から積まれた下の層の要求に勝つ()
        {
            // 要素の指の後に地の掴む手を取り直しても指のまま
            // Re-taking the surface grab after the element pointer keeps the pointer
            var element = _stack.Request(UiOsCursorShape.Pointer, UiOsCursorLayer.Element);
            var surface = _stack.Request(UiOsCursorShape.Grab, UiOsCursorLayer.Surface);
            CollectionAssert.AreEqual(new[] { UiOsCursorShape.Pointer }, _applier.AppliedShapes);

            // ドラッグの層は要素に勝ち、外すと要素へ、要素を外すと地へ戻る
            // The drag layer beats the element; withdrawing returns to the element, then to the surface
            var drag = _stack.Request(UiOsCursorShape.Grabbing, UiOsCursorLayer.Drag);
            drag.Dispose();
            element.Dispose();
            surface.Dispose();
            CollectionAssert.AreEqual(new[] { UiOsCursorShape.Pointer, UiOsCursorShape.Grabbing, UiOsCursorShape.Pointer, UiOsCursorShape.Grab, UiOsCursorShape.Default }, _applier.AppliedShapes);
        }

        [Test]
        public void 一番上が変わらない要求と取り下げでは当て直さない()
        {
            var top = _stack.Request(UiOsCursorShape.Grabbing, UiOsCursorLayer.Drag);
            var below = _stack.Request(UiOsCursorShape.Grab, UiOsCursorLayer.Surface);
            below.Dispose();
            CollectionAssert.AreEqual(new[] { UiOsCursorShape.Grabbing }, _applier.AppliedShapes);
            top.Dispose();
            CollectionAssert.AreEqual(new[] { UiOsCursorShape.Grabbing, UiOsCursorShape.Default }, _applier.AppliedShapes);
        }

        [Test]
        public void 二重のDisposeは別の要求を外さない()
        {
            // 同じ形・同じ層の要求が 2 つあるとき、片方の 2 回目の Dispose がもう片方を外さない
            // With two requests of the same shape and layer, a second Dispose of one never withdraws the other
            var first = _stack.Request(UiOsCursorShape.Grab, UiOsCursorLayer.Surface);
            var second = _stack.Request(UiOsCursorShape.Grab, UiOsCursorLayer.Surface);
            first.Dispose();
            first.Dispose();
            CollectionAssert.AreEqual(new[] { UiOsCursorShape.Grab }, _applier.AppliedShapes);
            second.Dispose();
            CollectionAssert.AreEqual(new[] { UiOsCursorShape.Grab, UiOsCursorShape.Default }, _applier.AppliedShapes);
        }
    }
}
